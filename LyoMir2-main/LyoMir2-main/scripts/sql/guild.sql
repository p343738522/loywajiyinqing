-- Guild 表：对齐 NativeSchemaProvisioner。全新库执行；已有生产库勿盲目 DROP。
CREATE DATABASE IF NOT EXISTS Guild;

CREATE TABLE IF NOT EXISTS Guild.Castle (
  Guid int unsigned NOT NULL DEFAULT 0,
  name char(64) binary NOT NULL DEFAULT '',
  TotalGold int unsigned NOT NULL DEFAULT 0,
  TodayIncome int unsigned NOT NULL DEFAULT 0,
  WineCount int unsigned NOT NULL DEFAULT 0,
  OwnGuild char(32) binary DEFAULT '',
  IncomeToday datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  changeDate datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  WarDate datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  Data blob,
  ExtValue1 int unsigned NOT NULL DEFAULT 0,
  ExtValue2 int unsigned NOT NULL DEFAULT 0,
  ExtValue3 int unsigned NOT NULL DEFAULT 0,
  ExtValue4 int unsigned NOT NULL DEFAULT 0,
  ExtValue5 int unsigned NOT NULL DEFAULT 0,
  ExtValue6 int unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (Guid)
);

CREATE TABLE IF NOT EXISTS Guild.guild_list (
  Idx int unsigned NOT NULL AUTO_INCREMENT,
  Gname char(32) binary NOT NULL DEFAULT '',
  MaxUser int unsigned NOT NULL DEFAULT 0,
  GLevel int unsigned NOT NULL DEFAULT 0,
  CurExp int unsigned NOT NULL DEFAULT 0,
  UserAddExp int unsigned NOT NULL DEFAULT 0,
  Water int unsigned NOT NULL DEFAULT 0,
  StarPoint int unsigned NOT NULL DEFAULT 0,
  StarGrantTime int unsigned NOT NULL DEFAULT 0,
  GuildFlag tinyint(1) unsigned NOT NULL DEFAULT 0,
  StarOwner char(15) binary DEFAULT '',
  CreateTime datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  Notice blob,
  ExtValue1 int unsigned NOT NULL DEFAULT 0,
  ExtValue2 int unsigned NOT NULL DEFAULT 0,
  ExtValue3 int unsigned NOT NULL DEFAULT 0,
  ExtValue4 int unsigned NOT NULL DEFAULT 0,
  ExtValue5 int unsigned NOT NULL DEFAULT 0,
  ExtValue6 int unsigned NOT NULL DEFAULT 0,
  PRIMARY KEY (Idx),
  UNIQUE KEY gname_index (Gname)
);

CREATE TABLE IF NOT EXISTS Guild.guild_rank (
  Idx int unsigned NOT NULL AUTO_INCREMENT,
  Gname char(32) binary NOT NULL DEFAULT '',
  RankID int unsigned NOT NULL DEFAULT 0,
  RankName char(16) binary NOT NULL DEFAULT '0',
  MaxUser int unsigned NOT NULL DEFAULT 0,
  CreateTime datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (Idx),
  UNIQUE KEY rankid_index (Gname, RankID),
  UNIQUE KEY rankname_index (Gname, RankName),
  KEY gname_index (Gname)
);

CREATE TABLE IF NOT EXISTS Guild.guild_user (
  Idx int unsigned NOT NULL AUTO_INCREMENT,
  Gname char(32) binary NOT NULL DEFAULT '',
  CharName char(16) binary NOT NULL DEFAULT '',
  RankID int unsigned NOT NULL DEFAULT 0,
  ConferRight int unsigned NOT NULL DEFAULT 0,
  Contribution int unsigned NOT NULL DEFAULT 0,
  JoinDate datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  job tinyint unsigned DEFAULT 0,
  sex tinyint unsigned DEFAULT 0,
  level smallint unsigned DEFAULT 0,
  modifydate datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  sfLevel smallint unsigned DEFAULT 0,
  PRIMARY KEY (Idx),
  UNIQUE KEY user_index (CharName),
  KEY rankid_index (Gname, RankID),
  KEY gname_index (Gname)
);

CREATE TABLE IF NOT EXISTS Guild.guild_relation (
  Idx int unsigned NOT NULL AUTO_INCREMENT,
  SrcGname char(32) binary NOT NULL DEFAULT '',
  DstGname char(32) binary NOT NULL DEFAULT '',
  Relationid tinyint(3) unsigned NOT NULL DEFAULT 0,
  ExtValue1 int unsigned NOT NULL DEFAULT 0,
  ExtValue2 int unsigned NOT NULL DEFAULT 0,
  CreateTime datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (Idx),
  UNIQUE KEY pair_index (SrcGname, DstGname, Relationid),
  KEY src_index (SrcGname, RelationID),
  KEY dst_index (DstGname, RelationID)
);

CREATE TABLE IF NOT EXISTS Guild.guild_log (
  Idx int unsigned NOT NULL AUTO_INCREMENT,
  Gname char(32) binary NOT NULL DEFAULT '',
  LogType int unsigned NOT NULL DEFAULT 0,
  MsgText char(200) binary NOT NULL DEFAULT '',
  CreateTime datetime NOT NULL DEFAULT '0000-00-00 00:00:00',
  PRIMARY KEY (Idx),
  KEY gname_index (Gname)
);
