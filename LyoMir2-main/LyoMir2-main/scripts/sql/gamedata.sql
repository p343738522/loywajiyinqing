-- gamedata 表：对齐 NativeSchemaProvisioner 与 DBSvr 实际查询列名。
-- 不含无原生 CREATE 的 CreditCard / mirStars / Kindling 等（缺 DDL 不造表）。
-- 全新库执行；已有生产库勿盲目 DROP。
CREATE DATABASE IF NOT EXISTS gamedata;

CREATE TABLE IF NOT EXISTS gamedata.ZongpaiBase (
  Idx int AUTO_INCREMENT PRIMARY KEY,
  MasterName Char(15) binary NOT NULL,
  MasterLevel smallint unsigned DEFAULT 0,
  StudentExp int unsigned DEFAULT 0,
  MasterExp int unsigned DEFAULT 0,
  UpdateTime DateTime NOT NULL,
  Notice blob,
  UNIQUE KEY MasterName_Index (MasterName),
  INDEX Order_Index (MasterLevel, StudentExp)
);

CREATE TABLE IF NOT EXISTS gamedata.ZongpaiRole (
  Idx int AUTO_INCREMENT PRIMARY KEY,
  MasterName varchar(15) binary NOT NULL,
  RoleName varchar(20) binary NOT NULL,
  RolePrivilege int unsigned DEFAULT 0,
  MaxMemberNum int DEFAULT 0,
  UNIQUE KEY RoleName_Index (MasterName, RoleName)
);

CREATE TABLE IF NOT EXISTS gamedata.ZongpaiMember (
  Idx int AUTO_INCREMENT PRIMARY KEY,
  MasterName varchar(15) binary NOT NULL,
  MemberName varchar(15) binary NOT NULL,
  RoleName varchar(20) binary NOT NULL,
  UNIQUE KEY MemberName_Index (MemberName),
  INDEX RoleName_Index (MasterName, RoleName)
);

CREATE TABLE IF NOT EXISTS gamedata.mirparams (
  idx int unsigned NOT NULL PRIMARY KEY AUTO_INCREMENT,
  ParamNo int NOT NULL UNIQUE,
  ParamName varchar(20) binary,
  g1 int(10) DEFAULT -1, g2 int(10) DEFAULT -1, g3 int(10) DEFAULT -1,
  g4 int(10) DEFAULT -1, g5 int(10) DEFAULT -1, g6 int(10) DEFAULT -1,
  g7 int(10) DEFAULT -1, g8 int(10) DEFAULT -1, g9 int(10) DEFAULT -1,
  g10 int(10) DEFAULT -1, g11 int(10) DEFAULT -1, g12 int(10) DEFAULT -1,
  g13 int(10) DEFAULT -1, g14 int(10) DEFAULT -1, g15 int(10) DEFAULT -1,
  g16 int(10) DEFAULT -1, g17 int(10) DEFAULT -1, g18 int(10) DEFAULT -1,
  g19 int(10) DEFAULT -1, g20 int(10) DEFAULT -1, g21 int(10) DEFAULT -1,
  g22 int(10) DEFAULT -1, g23 int(10) DEFAULT -1, g24 int(10) DEFAULT -1,
  g25 int(10) DEFAULT -1, g26 int(10) DEFAULT -1, g27 int(10) DEFAULT -1,
  g28 int(10) DEFAULT -1, g29 int(10) DEFAULT -1, g30 int(10) DEFAULT -1,
  g31 int(10) DEFAULT -1, g32 int(10) DEFAULT -1, g33 int(10) DEFAULT -1,
  g34 int(10) DEFAULT -1, g35 int(10) DEFAULT -1, g36 int(10) DEFAULT -1,
  g37 int(10) DEFAULT -1, g38 int(10) DEFAULT -1, g39 int(10) DEFAULT -1,
  g40 int(10) DEFAULT -1, g41 int(10) DEFAULT -1, g42 int(10) DEFAULT -1,
  g43 int(10) DEFAULT -1, g44 int(10) DEFAULT -1, g45 int(10) DEFAULT -1,
  g46 int(10) DEFAULT -1, g47 int(10) DEFAULT -1, g48 int(10) DEFAULT -1,
  g49 int(10) DEFAULT -1, g50 int(10) DEFAULT -1,
  INDEX ParamNo_Idx (ParamNo)
);

CREATE TABLE IF NOT EXISTS gamedata.TransferAreaScoreSendRecord (
  Idx int AUTO_INCREMENT PRIMARY KEY,
  TimeStamp DateTime NOT NULL,
  CharName Char(15) binary NOT NULL,
  ZoneId smallint unsigned DEFAULT 0,
  GroupId smallint unsigned DEFAULT 0,
  ScoreType smallint unsigned DEFAULT 0,
  Score smallint unsigned DEFAULT 0,
  State smallint unsigned DEFAULT 0,
  UNIQUE KEY Record_Index (TimeStamp, CharName, ZoneId, GroupId, ScoreType)
);

CREATE TABLE IF NOT EXISTS gamedata.TransferAreaScore (
  Idx int AUTO_INCREMENT PRIMARY KEY,
  CharName Char(15) binary NOT NULL,
  Score1 int DEFAULT 0,
  Score2 int DEFAULT 0,
  Score3 int DEFAULT 0,
  UNIQUE KEY Char_Index (CharName)
);

-- NativeSchemaProvisioner 把 HallOfFame 建在当前库（注释为 mir3.HallOfFame）。
-- C# MySqlNativeHallOfFameService 实际查询 gamedata.halloffame.CharData / Rank。
CREATE TABLE IF NOT EXISTS gamedata.HallOfFame (
  Idx int NOT NULL PRIMARY KEY AUTO_INCREMENT,
  Rank int DEFAULT 0,
  ZoneName char(20) binary NOT NULL DEFAULT '',
  GroupName char(20) binary NOT NULL DEFAULT '',
  CharName char(15) binary NOT NULL DEFAULT '',
  CharData blob
);
