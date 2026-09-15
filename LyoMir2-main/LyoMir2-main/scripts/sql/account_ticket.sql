-- C# 登录链 ticket 表（非原生 DBServer 字面量；LoginGate/GameGate fail-closed 查询用）。
-- 空 TicketDb / 缺行 / 过期 / 连库失败均不得放行。
CREATE DATABASE IF NOT EXISTS account;
CREATE TABLE IF NOT EXISTS account.ticket (
  ticket VARCHAR(64) BINARY NOT NULL,
  pt_id CHAR(20) BINARY NOT NULL,
  create_time BIGINT NOT NULL,
  PRIMARY KEY (ticket),
  KEY idx_create_time (create_time)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- 手工签发示例（create_time = Unix 秒；默认 5 分钟内有效）：
-- INSERT INTO account.ticket(ticket, pt_id, create_time)
-- VALUES ('PUT_TICKET_HERE', 'ptid0000000000000001', UNIX_TIMESTAMP());

-- BaiZhu HTTP /account (LoginGate AccountHttpListen) also reads account.normal.
-- Empty TicketDb still fail-closed; this table is not auto-created.
-- CREATE TABLE IF NOT EXISTS account.normal (
--   pt_id CHAR(20) BINARY NOT NULL,
--   uid VARCHAR(64) BINARY NOT NULL,
--   password VARCHAR(64),
--   safecode VARCHAR(64),
--   login_time DATETIME,
--   create_time DATETIME,
--   PRIMARY KEY (pt_id),
--   UNIQUE KEY uk_uid (uid)
-- ) ENGINE=InnoDB DEFAULT CHARSET=latin1;
