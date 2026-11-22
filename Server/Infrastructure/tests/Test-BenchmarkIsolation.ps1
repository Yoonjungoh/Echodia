param([Parameter(Mandatory)][string]$AssemblyDirectory)
$ErrorActionPreference='Stop'
# Run this script in its own PowerShell 7 process; only synthetic QA credentials are used.
$provider=(Get-ChildItem (Join-Path $AssemblyDirectory 'runtimes/win/lib') -Recurse -Filter Microsoft.Data.SqlClient.dll | Select-Object -First 1).FullName
[void][Reflection.Assembly]::LoadFrom($provider)
foreach($dll in 'System.Security.Cryptography.ProtectedData.dll','Server.dll'){[void][Reflection.Assembly]::LoadFrom((Join-Path $AssemblyDirectory $dll))}
$file=Join-Path ([IO.Path]::GetTempPath()) ([guid]::NewGuid().ToString('N')+'.qa-fixture.dpapi')
$oldRun=$env:ECHODIA_QA_RUN;$oldPath=$env:ECHODIA_DB_CONNECTION_DPAPI_FILE;$oldSql=$env:DB_CONNECTION_STRING
try{
    $env:ECHODIA_QA_RUN='Synthetic1';$env:ECHODIA_DB_CONNECTION_DPAPI_FILE=$file;$env:DB_CONNECTION_STRING=$null
    $ok='Initial Catalog=EchodiaPerf_Synthetic1;Data Source=tcp:10.0.0.5,1433;Encrypt=True;TrustServerCertificate=False;User ID=fixture;Password=synthetic'
    $cases=@(
      @($ok,$true),
      @('Initial Catalog=EchodiaPerf_Synthetic1;Data Source=lpc:(local)\SQLEXPRESS;Encrypt=False;Integrated Security=True',$true),
      @($ok.Replace('EchodiaPerf_Synthetic1','GameDB'),$false),
      @($ok.Replace('EchodiaPerf_Synthetic1','EchodiaPerf_Other'),$false),
      @($ok.Replace('Encrypt=True','Encrypt=False'),$false),
      @($ok.Replace('TrustServerCertificate=False','TrustServerCertificate=True'),$false),
      @(($ok+';AttachDBFilename=C:\fixture.mdf'),$false),
      @(($ok+';Failover Partner=other'),$false))
    foreach($test in $cases){
        $bytes=[Text.Encoding]::UTF8.GetBytes($test[0])
        [IO.File]::WriteAllBytes($file,[Security.Cryptography.ProtectedData]::Protect($bytes,$null,'LocalMachine'))
        $pass=$true
        try{$key=[Server.DB.BenchmarkDatabase]::Activate()}catch{$pass=$false; $err=$_.Exception.GetBaseException().Message}
        if($pass -ne $test[1] -or ($pass -and $key -cne 'EchodiaPerf:Synthetic1:loggedIn')){throw "Unexpected QA isolation validation result: $err"}
    }
    Write-Output ('Passed '+$cases.Count+' synthetic database and Redis-key validation cases.')
}finally{
    $env:ECHODIA_QA_RUN=$oldRun;$env:ECHODIA_DB_CONNECTION_DPAPI_FILE=$oldPath;$env:DB_CONNECTION_STRING=$oldSql
    if(Test-Path -LiteralPath $file){Remove-Item -LiteralPath $file}
}
