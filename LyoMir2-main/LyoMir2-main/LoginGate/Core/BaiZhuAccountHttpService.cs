using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace LoginGate.Core;

/// <summary>
/// BaiZhu G2.5 LoginCenter HTTP on def.gatePort (default 8088).
/// POST/GET /account with no id → server list; id+psw → ticket.
/// Empty TicketDb never issues a ticket.
/// </summary>
internal sealed class BaiZhuAccountHttpService
{
    private const int MaximumHeaderBytes = 8192;
    private const int MaximumBodyBytes = 65536;
    private readonly LoginGateConfig _config;
    private readonly IBaiZhuAccountStore _accounts;
    private readonly Action<string, string> _log;
    private readonly object _lifecycle = new();
    private CancellationTokenSource? _stop;
    private TcpListener? _listener;
    private Task? _acceptTask;

    public BaiZhuAccountHttpService(LoginGateConfig config, IBaiZhuAccountStore accounts,
        Action<string, string> log)
    {
        _config = config;
        _accounts = accounts;
        _log = log;
    }

    public int BoundPort { get; private set; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lifecycle)
        {
            if (_listener != null) return Task.CompletedTask;
            _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _listener = new TcpListener(IPAddress.Any, _config.AccountHttpListen);
            _listener.Start(64);
            BoundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _acceptTask = AcceptLoopAsync(_listener, _stop.Token);
        }
        _log("INFO",
            $"白猪 HTTP 登录监听 :{BoundPort}  （客户端 def.gatePort / /account）");
        _log("INFO", "HTTP 票据：" + _accounts.DescribeSource());
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? task;
        CancellationTokenSource? stop;
        lock (_lifecycle)
        {
            if (_listener == null) return;
            stop = _stop;
            task = _acceptTask;
            _stop = null;
            _acceptTask = null;
            try { stop?.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            _listener = null;
            BoundPort = 0;
        }
        if (task != null)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) when (stop?.IsCancellationRequested == true) { }
        }
        stop?.Dispose();
    }

    internal static string BuildServerListJson(LoginGateConfig config, string advertiseHost,
        int loginGateListen)
    {
        var host = string.IsNullOrWhiteSpace(advertiseHost) ? "127.0.0.1" : advertiseHost.Trim();
        var zoneIp = loginGateListen == LoginGateConfig.DefaultLoginGateListen
            ? host
            : $"{host}:{loginGateListen}";
        const int clientVerId = 185;
        var verInfo = new List<object>
        {
            new { verid = clientVerId, vername = "1.85", clientver = clientVerId }
        };
        var servers = new List<object>();
        foreach (var area in config.GetConfiguredAreas())
        {
            foreach (var group in area.Groups.OrderBy(item => item.Slot))
            {
                servers.Add(new
                {
                    verid = clientVerId,
                    zoneid = group.Index == 0 ? group.Slot : group.Index,
                    zonename = group.Name,
                    name = group.Name,
                    zoneip = zoneIp,
                    area = area.AreaIdx,
                    suggest = group.Slot == 1 ? 1 : 0,
                    heat = 0,
                    isactive = 1,
                    serverinfo = "",
                    ConfigName = (string?)null,
                    ConfigVer = (string?)null
                });
            }
        }

        var payload = new
        {
            serverlist = new
            {
                kaifubiao = 0,
                imglist = Array.Empty<object>(),
                verinfo = verInfo,
                shopurl = "",
                notice = "",
                servers,
                forces = new { }
            },
            last = "null",
            stime = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        return JsonSerializer.Serialize(payload);
    }

    internal static string BuildLoginJson(BaiZhuAccountResult result, string serverListJson)
    {
        if (result.Code != 0 || string.IsNullOrEmpty(result.Ticket))
        {
            return JsonSerializer.Serialize(new
            {
                code = result.Code == 0 ? -1 : result.Code,
                des = string.IsNullOrEmpty(result.Description) ? "认证失败" : result.Description
            });
        }

        JsonElement list;
        using (var document = JsonDocument.Parse(serverListJson))
        {
            list = document.RootElement.TryGetProperty("serverlist", out var serverList)
                ? serverList.Clone()
                : document.RootElement.Clone();
        }

        return JsonSerializer.Serialize(new
        {
            code = 0,
            des = "成功",
            phone = "0",
            last = string.IsNullOrEmpty(result.LastServer) ? "null" : result.LastServer,
            ticket = result.Ticket,
            list
        });
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                client.NoDelay = true;
                var accepted = client;
                client = null;
                _ = Task.Run(() => HandleClientAsync(accepted, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                client?.Dispose();
                break;
            }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
            {
                client?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                client?.Dispose();
                _log("WARN", "白猪 HTTP accept: " + ex.Message);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                var stream = client.GetStream();
                var request = await ReadHttpRequestAsync(stream, timeout.Token).ConfigureAwait(false);
                if (request == null)
                {
                    await WriteHttpAsync(stream, 400, "{\"code\":-1,\"des\":\"bad request\"}",
                        timeout.Token).ConfigureAwait(false);
                    return;
                }

                var form = ParseForm(request);
                var json = await DispatchAsync(request.Path, form, request.Host, timeout.Token)
                    .ConfigureAwait(false);
                await WriteHttpAsync(stream, 200, json, timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (Exception ex)
            {
                _log("WARN", "白猪 HTTP: " + ex.Message);
            }
        }
    }

    private async Task<string> DispatchAsync(string path, Dictionary<string, string> form,
        string host, CancellationToken cancellationToken)
    {
        var route = path.Trim('/');
        var slash = route.IndexOf('/');
        if (slash >= 0) route = route[..slash];
        route = route.ToLowerInvariant();
        var serverList = BuildServerListJson(_config, ResolveAdvertiseHost(host),
            _config.LoginGateListen);

        switch (route)
        {
            case "":
                return "hello world";
            case "account":
                return await HandleAccountAsync(form, serverList, cancellationToken)
                    .ConfigureAwait(false);
            case "reg":
                return JsonFromResult(await _accounts.RegisterAsync(
                    Get(form, "id"), Get(form, "psw"), Get(form, "safecode"),
                    cancellationToken).ConfigureAwait(false));
            case "modifypsw":
                return JsonFromResult(await _accounts.ChangePasswordAsync(
                    Get(form, "id"), Get(form, "psw"), Get(form, "safecode"),
                    cancellationToken).ConfigureAwait(false));
            case "bind":
                return JsonFromResult(await _accounts.BindAsync(
                    FirstNonEmpty(Get(form, "machineid"), Get(form, "machine_id"), Get(form, "id")),
                    FirstNonEmpty(Get(form, "new_id"), Get(form, "newid"), Get(form, "id")),
                    Get(form, "psw"), Get(form, "safecode"),
                    cancellationToken).ConfigureAwait(false));
            case "serverlist":
                return serverList;
            default:
                return "{\"code\":-1,\"des\":\"未知请求\"}";
        }
    }

    private async Task<string> HandleAccountAsync(Dictionary<string, string> form,
        string serverList, CancellationToken cancellationToken)
    {
        var id = Get(form, "id");
        if (string.IsNullOrEmpty(id))
            return serverList;

        var guest = Get(form, "guest") == "1";
        var result = await _accounts.LoginAsync(id, Get(form, "psw"), guest, cancellationToken)
            .ConfigureAwait(false);
        _log("INFO", guest
            ? "HTTP /account guest fail-closed or login"
            : $"HTTP /account id={id} code={result.Code}");
        return BuildLoginJson(result, serverList);
    }

    private string ResolveAdvertiseHost(string requestHost)
    {
        if (!string.IsNullOrWhiteSpace(_config.AdvertiseHost))
            return _config.AdvertiseHost.Trim();
        if (string.IsNullOrWhiteSpace(requestHost))
            return "127.0.0.1";
        var host = requestHost.Trim();
        if (host.StartsWith('[') && host.Contains(']'))
        {
            var close = host.IndexOf(']');
            return host[1..close];
        }
        var colon = host.LastIndexOf(':');
        if (colon > 0 && host.IndexOf(':') == colon)
            return host[..colon];
        return host;
    }

    private static string JsonFromResult(BaiZhuAccountResult result) =>
        JsonSerializer.Serialize(new
        {
            code = result.Code,
            des = result.Description ?? string.Empty
        });

    private static string Get(Dictionary<string, string> form, string key) =>
        form.TryGetValue(key, out var value) ? value : string.Empty;

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrEmpty(value)) return value;
        }
        return string.Empty;
    }

    private static Dictionary<string, string> ParseForm(HttpRequest request)
    {
        var form = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddQuery(form, request.Query);
        if (!string.IsNullOrEmpty(request.Body))
            AddQuery(form, request.Body);
        return form;
    }

    private static void AddQuery(Dictionary<string, string> form, string query)
    {
        if (string.IsNullOrEmpty(query)) return;
        if (query.StartsWith('?')) query = query[1..];
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
            {
                form[Uri.UnescapeDataString(pair.Replace('+', ' '))] = string.Empty;
                continue;
            }
            var key = Uri.UnescapeDataString(pair[..eq].Replace('+', ' '));
            var value = Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            form[key] = value;
        }
    }

    private static async Task<HttpRequest?> ReadHttpRequestAsync(NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[MaximumHeaderBytes];
        var length = 0;
        var headerEnd = -1;
        while (length < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length, buffer.Length - length),
                cancellationToken).ConfigureAwait(false);
            if (read <= 0) return null;
            length += read;
            headerEnd = FindHeaderEnd(buffer, length);
            if (headerEnd >= 0) break;
        }
        if (headerEnd < 0) return null;

        var headerText = Encoding.ASCII.GetString(buffer, 0, headerEnd);
        var lines = headerText.Split("\r\n");
        if (lines.Length == 0) return null;
        var requestLine = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (requestLine.Length < 2) return null;
        var target = requestLine[1];
        var path = target;
        var query = string.Empty;
        var q = target.IndexOf('?');
        if (q >= 0)
        {
            path = target[..q];
            query = target[(q + 1)..];
        }

        var contentLength = 0;
        var host = string.Empty;
        foreach (var line in lines.Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var name = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(value, out var parsed))
                contentLength = parsed;
            else if (name.Equals("Host", StringComparison.OrdinalIgnoreCase))
                host = value;
        }
        if (contentLength < 0 || contentLength > MaximumBodyBytes) return null;

        var bodyOffset = headerEnd + 4;
        var bodyBytes = new byte[contentLength];
        var copied = Math.Max(0, Math.Min(contentLength, length - bodyOffset));
        if (copied > 0)
            Buffer.BlockCopy(buffer, bodyOffset, bodyBytes, 0, copied);
        while (copied < contentLength)
        {
            var read = await stream.ReadAsync(bodyBytes.AsMemory(copied), cancellationToken)
                .ConfigureAwait(false);
            if (read <= 0) return null;
            copied += read;
        }

        var body = contentLength == 0
            ? string.Empty
            : Encoding.UTF8.GetString(bodyBytes);
        return new HttpRequest(path, query, host, body);
    }

    private static int FindHeaderEnd(byte[] buffer, int length)
    {
        for (var i = 0; i + 3 < length; i++)
        {
            if (buffer[i] == (byte)'\r' && buffer[i + 1] == (byte)'\n' &&
                buffer[i + 2] == (byte)'\r' && buffer[i + 3] == (byte)'\n')
                return i;
        }
        return -1;
    }

    private static async Task WriteHttpAsync(NetworkStream stream, int status, string json,
        CancellationToken cancellationToken)
    {
        var body = Encoding.UTF8.GetBytes(json ?? string.Empty);
        var header =
            $"HTTP/1.1 {status} {(status == 200 ? "OK" : "Error")}\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Connection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed record HttpRequest(string Path, string Query, string Host, string Body);
}
