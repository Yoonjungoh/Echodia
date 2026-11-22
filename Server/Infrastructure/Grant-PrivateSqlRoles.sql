SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'GameDB' THROW 51001, 'Expected GameDB.', 1;
BEGIN TRANSACTION;
-- Only the restored, fully converted, split production database runs this script.
-- Existing accounts have already been migrated. Reject future malformed writes at their source.
IF OBJECT_ID(N'dbo.CK_Account_ProtectedPasswordV1',N'C') IS NULL
    ALTER TABLE dbo.Account WITH CHECK ADD CONSTRAINT CK_Account_ProtectedPasswordV1
        CHECK (AccountPassword IS NULL AND PasswordHashV1 IS NOT NULL AND DATALENGTH(PasswordHashV1)=48);
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE parent_object_id=OBJECT_ID(N'dbo.Account') AND
           name=N'CK_Account_ProtectedPasswordV1' AND (is_disabled=1 OR is_not_trusted=1))
    ALTER TABLE dbo.Account WITH CHECK CHECK CONSTRAINT CK_Account_ProtectedPasswordV1;
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id=OBJECT_ID(N'dbo.Account') AND name=N'CK_Account_ProtectedPasswordV1'
      AND is_disabled=0 AND is_not_trusted=0 AND is_not_for_replication=0
      AND LOWER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(definition,N'[',N''),N']',N''),N'(',N''),N')',N''),N' ',N''))
          =N'accountpasswordisnullandpasswordhashv1isnotnullanddatalengthpasswordhashv1=48'
) THROW 51005, 'The account credential constraint is missing, untrusted or has the wrong definition.',1;

IF DATABASE_PRINCIPAL_ID(N'EchodiaGameAccess') IS NULL CREATE ROLE [EchodiaGameAccess] AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'EchodiaLoginAccess') IS NULL CREATE ROLE [EchodiaLoginAccess] AUTHORIZATION dbo;
IF DATABASE_PRINCIPAL_ID(N'EchodiaGameRuntime') IS NULL CREATE USER [EchodiaGameRuntime] FOR LOGIN [EchodiaGameRuntime];
IF DATABASE_PRINCIPAL_ID(N'EchodiaLoginRuntime') IS NULL CREATE USER [EchodiaLoginRuntime] FOR LOGIN [EchodiaLoginRuntime];
IF EXISTS (
    SELECT 1 FROM sys.database_principals
    WHERE name IN (N'EchodiaGameRuntime',N'EchodiaLoginRuntime') AND (type <> 'S' OR sid <> SUSER_SID(name))
) THROW 51002, 'An existing database user belongs to a different login.', 1;

GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.Player TO [EchodiaGameAccess];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.PlayerItem TO [EchodiaGameAccess];
GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo.Quest TO [EchodiaGameAccess];
GRANT SELECT ON OBJECT::dbo.Account (AccountDbId,AccountId,AccountPassword,PasswordHashV1) TO [EchodiaLoginAccess];
GRANT INSERT ON OBJECT::dbo.Account TO [EchodiaLoginAccess];
GRANT UPDATE ON OBJECT::dbo.Account (AccountPassword,PasswordHashV1) TO [EchodiaLoginAccess];
IF IS_ROLEMEMBER(N'EchodiaGameAccess',N'EchodiaGameRuntime') <> 1 ALTER ROLE [EchodiaGameAccess] ADD MEMBER [EchodiaGameRuntime];
IF IS_ROLEMEMBER(N'EchodiaLoginAccess',N'EchodiaLoginRuntime') <> 1 ALTER ROLE [EchodiaLoginAccess] ADD MEMBER [EchodiaLoginRuntime];

EXECUTE AS LOGIN=N'EchodiaGameRuntime';
DECLARE @game_ok bit=0;
IF HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL')=0 AND HAS_PERMS_BY_NAME(NULL,NULL,'CONTROL SERVER')=0
   AND HAS_PERMS_BY_NAME('dbo.Player','OBJECT','SELECT')=1 AND HAS_PERMS_BY_NAME('dbo.Quest','OBJECT','UPDATE')=1
   AND HAS_PERMS_BY_NAME('dbo.PlayerItem','OBJECT','INSERT')=1
   AND ISNULL(HAS_PERMS_BY_NAME('dbo.Account','OBJECT','SELECT','PasswordHashV1','COLUMN'),0)=0
   AND ISNULL(HAS_PERMS_BY_NAME('dbo.Account','OBJECT','SELECT','AccountPassword','COLUMN'),0)=0
   AND ISNULL(HAS_PERMS_BY_NAME('dbo.Account','OBJECT','UPDATE','PasswordHashV1','COLUMN'),0)=0
   AND ISNULL(HAS_PERMS_BY_NAME('dbo.Account','OBJECT','INSERT'),0)=0 SET @game_ok=1;
REVERT;
IF @game_ok=0 THROW 51003, 'Game role permission boundary failed.',1;

EXECUTE AS LOGIN=N'EchodiaLoginRuntime';
DECLARE @login_ok bit=0;
IF HAS_PERMS_BY_NAME(DB_NAME(),'DATABASE','CONTROL')=0 AND HAS_PERMS_BY_NAME(NULL,NULL,'CONTROL SERVER')=0
   AND HAS_PERMS_BY_NAME('dbo.Account','OBJECT','SELECT','PasswordHashV1','COLUMN')=1
   AND HAS_PERMS_BY_NAME('dbo.Account','OBJECT','INSERT')=1
   AND HAS_PERMS_BY_NAME('dbo.Account','OBJECT','UPDATE','PasswordHashV1','COLUMN')=1
   AND HAS_PERMS_BY_NAME('dbo.Account','OBJECT','UPDATE','AccountId','COLUMN')=0
   AND HAS_PERMS_BY_NAME('dbo.Account','OBJECT','DELETE')=0
   AND ISNULL(HAS_PERMS_BY_NAME('dbo.Player','OBJECT','SELECT'),0)=0
   AND ISNULL(HAS_PERMS_BY_NAME('dbo.PlayerItem','OBJECT','UPDATE'),0)=0
   AND ISNULL(HAS_PERMS_BY_NAME('dbo.Quest','OBJECT','UPDATE'),0)=0 SET @login_ok=1;
REVERT;
IF @login_ok=0 THROW 51004, 'Login role permission boundary failed.',1;
COMMIT;
