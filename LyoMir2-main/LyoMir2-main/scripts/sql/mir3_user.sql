-- 角色库：对齐 NativeSchemaProvisioner 的 user/hero/storage/award/HallOfFame/dominatorpet。
-- LoginID DEFAULT ''：CreateCharacterCore INSERT 不写该列，STRICT 下需默认空串；不是登录放行。
-- 不包含完整 Envir/Map。全新库执行；已有生产库勿盲目 DROP。
CREATE DATABASE IF NOT EXISTS mir3;
CREATE DATABASE IF NOT EXISTS mir3_backup;

CREATE TABLE IF NOT EXISTS mir3.user_index (
  idx int unsigned NOT NULL AUTO_INCREMENT PRIMARY KEY,
  PTID Char(20) binary NOT NULL,
  LoginID Char(15) binary NOT NULL DEFAULT '',
  ChrName Char(15) binary NOT NULL UNIQUE,
  IsDelete Bool DEFAULT 0,
  IsSelect Bool DEFAULT 0,
  job tinyint unsigned DEFAULT 0,
  sex tinyint unsigned DEFAULT 0,
  level smallint unsigned DEFAULT 0,
  Exp int unsigned DEFAULT 0,
  PlatinaChrLv tinyint unsigned DEFAULT 0,
  HeroCardLv tinyint unsigned DEFAULT 0,
  ApprenticeNum int unsigned DEFAULT 0,
  GuardNum int unsigned DEFAULT 0,
  DarePoint int DEFAULT 0,
  CreateDate DateTime DEFAULT '2000-01-01',
  ModifyDate DateTime DEFAULT '2000-01-01',
  AdminLevel tinyint unsigned DEFAULT 0,
  ForceLv smallint unsigned DEFAULT 0,
  ForceExp int unsigned DEFAULT 0,
  FightPoints int unsigned DEFAULT 0,
  sfLevel int DEFAULT 0,
  SrcZoneId smallint unsigned DEFAULT 0,
  SrcGroupId smallint unsigned DEFAULT 0,
  SrcCharName Char(15) binary DEFAULT '',
  DesZoneId smallint unsigned DEFAULT 0,
  DesGroupId smallint unsigned DEFAULT 0,
  UserId bigint DEFAULT 0,
  IsTransLock smallint DEFAULT 0,
  TransferModal smallint DEFAULT 0,
  lvChangeTime DateTime DEFAULT '2100-1-1',
  INDEX UserId_index (UserId),
  INDEX PTID_index (PTID, ChrName, job, level, Exp),
  INDEX Date_index (ModifyDate, Job, Level, Exp),
  INDEX Level_index (Level, ChrName),
  INDEX level_exp_sort (job, level, Exp),
  INDEX DarePonit_Sort (DarePoint, level),
  INDEX Admin_Level_Sort (AdminLevel, level, exp),
  INDEX Job_index (Job, ChrName)
);

CREATE TABLE IF NOT EXISTS mir3.user_data (
  Idx Int unsigned NOT NULL PRIMARY KEY,
  ChrName Char(15) binary NOT NULL UNIQUE,
  Status TinyInt unsigned DEFAULT 0,
  Data Blob,
  ScriptData Blob,
  INDEX Name_Index (ChrName)
);

CREATE TABLE IF NOT EXISTS mir3.hero_index (
  idx INT unsigned NOT NULL AUTO_INCREMENT PRIMARY KEY,
  HeroName Char(15) binary NOT NULL UNIQUE,
  MasterName Char(15) binary NOT NULL,
  IsDelete Bool DEFAULT 0,
  HeroType tinyint Unsigned DEFAULT 1,
  job tinyint unsigned DEFAULT 0,
  sex tinyint unsigned DEFAULT 0,
  Level smallint unsigned DEFAULT 0,
  Exp int unsigned DEFAULT 0,
  Consignation tinyint Unsigned DEFAULT 0,
  CreateDate DateTime DEFAULT '2000-01-01',
  ModifyDate DateTime DEFAULT '2000-01-01',
  ForceLv smallint unsigned DEFAULT 0,
  ForceExp int unsigned DEFAULT 0,
  sfLevel int DEFAULT 0,
  SrcZoneId smallint unsigned DEFAULT 0,
  SrcGroupId smallint unsigned DEFAULT 0,
  SrcHeroName Char(15) binary DEFAULT '',
  heroId bigint DEFAULT 0,
  lvChangeTime DateTime DEFAULT '2100-1-1',
  INDEX heroId_index (heroId),
  INDEX hero_index (HeroName),
  INDEX Master_index (MasterName, HeroName, IsDelete),
  INDEX Data_index (ModifyDate, Level),
  INDEX Level_index (Level, HeroName),
  INDEX level_exp_sort (job, level, Exp),
  INDEX Job_index (Job, HeroName)
);

CREATE TABLE IF NOT EXISTS mir3.hero_data (
  Idx Int unsigned NOT NULL PRIMARY KEY,
  HeroName Char(15) binary NOT NULL UNIQUE,
  Status TinyInt unsigned DEFAULT 0,
  Data Blob,
  dynData Blob,
  NameLayout TINYINT NOT NULL DEFAULT 0 COMMENT '0=unknown 1=csharp-swapped 2=native-correct',
  INDEX Name_Index (HeroName)
);

CREATE TABLE IF NOT EXISTS mir3.user_storage (
  Idx int AUTO_INCREMENT PRIMARY KEY,
  PTID Char(20) binary NOT NULL UNIQUE,
  Data blob,
  INDEX PTID_Index (PTID)
);

CREATE TABLE IF NOT EXISTS mir3.awardplayers (
  Idx int AUTO_INCREMENT PRIMARY KEY,
  PTID char(20) binary NOT NULL UNIQUE,
  HumName Char(14) binary NOT NULL DEFAULT '',
  Level smallint unsigned DEFAULT 0,
  job tinyint unsigned DEFAULT 0,
  sex tinyint unsigned DEFAULT 0,
  Status tinyint unsigned DEFAULT 0,
  INDEX PTID_Index (PTID, Status)
);

CREATE TABLE IF NOT EXISTS mir3.HallOfFame (
  Idx int NOT NULL PRIMARY KEY AUTO_INCREMENT,
  Rank int DEFAULT 0,
  ZoneName char(20) binary NOT NULL DEFAULT '',
  GroupName char(20) binary NOT NULL DEFAULT '',
  CharName char(15) binary NOT NULL DEFAULT '',
  CharData blob
);

CREATE TABLE IF NOT EXISTS mir3.dominatorpet (
  Idx int AUTO_INCREMENT PRIMARY KEY,
  MasterId bigInt NOT NULL UNIQUE,
  MasterName Char(15) binary NOT NULL,
  Level int(5) NOT NULL,
  Exp int(10) unsigned NOT NULL,
  Data Blob,
  CreateDate DateTime DEFAULT '2000-01-01',
  ModifyDate DateTime DEFAULT '2000-01-01',
  INDEX MasterId_Index (MasterId),
  INDEX MasterName_Index (MasterName)
);

CREATE TABLE IF NOT EXISTS mir3_backup.user_index LIKE mir3.user_index;
CREATE TABLE IF NOT EXISTS mir3_backup.user_data LIKE mir3.user_data;
CREATE TABLE IF NOT EXISTS mir3_backup.hero_index LIKE mir3.hero_index;
CREATE TABLE IF NOT EXISTS mir3_backup.hero_data LIKE mir3.hero_data;
CREATE TABLE IF NOT EXISTS mir3_backup.user_storage LIKE mir3.user_storage;
CREATE TABLE IF NOT EXISTS mir3_backup.dominatorpet LIKE mir3.dominatorpet;
