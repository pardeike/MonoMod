param (
    [Parameter(Mandatory=$true, Position=0)]
    [string]$Exe,
    [Parameter(Mandatory=$false, Position=1, ValueFromRemainingArguments=$True)]
    [string[]]$Args = @()
)

$ErrorActionPreference = 'Stop';

$dumpsPath = $env:DUMPS_PATH;
if ($null -eq $dumpsPath)
{
    Write-Error "DUMPS_PATH not set!";
}

# make sure the dir exists
New-Item -Type Directory $dumpsPath -Force | Out-Null;

if ($IsWindows)
{
    # on Windows, we need to configure some registry keys before invoking
    $key = "HKLM:\\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps";
    New-Item -Path $key -ErrorAction SilentlyContinue;
    New-ItemProperty -Path $key -Name 'DumpType' -PropertyType 'DWord' -Value 2 -Force;
    New-ItemProperty -Path $key -Name 'DumpCount' -PropertyType 'DWord' -Value 10 -Force;
    New-ItemProperty -Path $key -Name 'DumpFolder' -PropertyType 'String' -Value $dumpsPath -Force;

    # then we can execute the program
    &$Exe @Args;
    exit $LastExitCode;
}
elseif ($IsLinux)
{
    # on Linux, we need to set the core_pattern and run the app with a ulimit -c unlimited
    $corePattern = Join-Path $dumpsPath "dump_%e_%p.core";
    Write-Output ($Args -join "`n") | bash -c @"
set -eo pipefail;
echo "\n$corePattern" | sudo -S tee /proc/sys/kernel/core_pattern;
ulimit -c unlimited;
ulimit -t 600; # hard-limit the program to take no more than 10 minutes (nothing we will use this for needs anywhere near that much; any more is a problem)
set +e;
xargs "$Exe";
exit `$?;
"@;
    exit $LastExitCode;

}
elseif ($IsMacOS)
{
    $corePattern = Join-Path $dumpsPath "dump_%N_%P.core";
    Write-Output ($Args -join "`n") | bash -c @"
set -eo pipefail;
sudo sysctl kern.coredump=1;
sysctl "kern.corefile=$corePattern";
ulimit -c unlimited;
ulimit -t 600; # hard-limit the program to take no more than 10 minutes (nothing we will use this for needs anywhere near that much; any more is a problem)
set +e;
xargs lldb -o "process handle SIGXCPU --stop true --notify true" -o "run" -k "process save-core -s full -- $(Join-Path $dumpsPath 'dump_timeout.core')" -k "kill" -o "quit" -- "$Exe";
exit `$?;
"@;
    exit $LastExitCode;
}
else
{
    Write-Error "Unknown operating system; not proceeding"
}
