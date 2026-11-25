param(
    [string]$Exe = ''
)

$ErrorActionPreference = 'Stop'

$dotnetRoot = 'C:\Users\Lenovo\AppData\Local\Microsoft\dotnet'
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_ROOT_X64 = $dotnetRoot

$repoRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($Exe)) {
    $Exe = Join-Path $repoRoot 'artifacts\Release\SuratGenerator\SuratGenerator.Desktop.exe'
}
if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) {
    $Exe = Join-Path $repoRoot 'SuratGenerator.Desktop\bin\x64\Release\net10.0-windows10.0.19041.0\win-x64\SuratGenerator.Desktop.exe'
}
if (-not (Test-Path -LiteralPath $Exe -PathType Leaf)) {
    throw "Executable was not found: $Exe"
}

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class SmokeWindow {
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
    public static void Click(IntPtr hWnd, int x, int y) {
        SetForegroundWindow(hWnd);
        SetCursorPos(x, y);
        mouse_event(0x0002, 0, 0, 0, IntPtr.Zero);
        mouse_event(0x0004, 0, 0, 0, IntPtr.Zero);
    }
}
'@

$tempRoot = Join-Path $env:TEMP 'SuratGeneratorSmoke'
New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null
$stdoutPath = Join-Path $tempRoot 'stdout.txt'
$stderrPath = Join-Path $tempRoot 'stderr.txt'
Remove-Item -LiteralPath $stdoutPath, $stderrPath -ErrorAction SilentlyContinue

$wordBefore = @(Get-Process -Name WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id }) -join ','
$process = Start-Process -FilePath $Exe -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
$failures = New-Object System.Collections.Generic.List[string]

function Find-FirstElement {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [string] $Name,
        [System.Windows.Automation.ControlType] $ControlType
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::NameProperty,
        $Name)
    $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    foreach ($candidate in $matches) {
        if ($candidate.Current.ControlType -eq $ControlType) {
            return $candidate
        }
    }
    return $null
}

function Click-Element {
    param(
        [System.Windows.Automation.AutomationElement] $Element,
        [IntPtr] $Handle
    )

    $rect = $Element.Current.BoundingRectangle
    [SmokeWindow]::Click($Handle, [int]($rect.X + $rect.Width / 2), [int]($rect.Y + $rect.Height / 2))
}

for ($i = 0; $i -lt 60 -and $process.MainWindowHandle -eq [IntPtr]::Zero; $i++) {
    Start-Sleep -Milliseconds 500
    $process.Refresh()
}
if ($process.MainWindowHandle -eq [IntPtr]::Zero) {
    $failures.Add('Window did not start within 30 seconds.')
    $process.Kill()
    $process.WaitForExit()
} else {
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    [SmokeWindow]::MoveWindow($process.MainWindowHandle, 80, 80, 1600, 900, $true) | Out-Null
    Start-Sleep -Seconds 1
    $window = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
    $pages = @(
        @{ Name = 'Buat Surat'; Marker = '' },
        @{ Name = 'Template'; Marker = 'Pindai Folder' },
        @{ Name = 'Master Struktur Folder'; Marker = 'Tambah Kategori' },
        @{ Name = 'Master Dokumen'; Marker = 'Unggah File' },
        @{ Name = 'Manajemen Pekerjaan'; Marker = 'Buat Pekerjaan' },
        @{ Name = 'Riwayat Surat'; Marker = '' },
        @{ Name = 'Draft'; Marker = '' },
        @{ Name = 'Panduan'; Marker = 'Fungsi Menu' },
        @{ Name = 'Pengaturan'; Marker = 'Pilih Folder' }
    )

    foreach ($page in $pages) {
        $condition = New-Object System.Windows.Automation.PropertyCondition(
            [System.Windows.Automation.AutomationElement]::NameProperty,
            $page.Name)
        $matches = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
        $target = $null
        foreach ($candidate in $matches) {
            if ($candidate.Current.ControlType -eq [System.Windows.Automation.ControlType]::DataItem) {
                $target = $candidate
                break
            }
        }

        $process.Refresh()
        if ($process.HasExited) {
            $failures.Add("Process exited before navigating to $($page.Name).")
            break
        }
        if ($null -eq $target) {
            $failures.Add("Navigation item was not found: $($page.Name)")
            continue
        }

        $rect = $target.Current.BoundingRectangle
        [SmokeWindow]::Click(
            $process.MainWindowHandle,
            [int]($rect.X + $rect.Width / 2),
            [int]($rect.Y + $rect.Height / 2))
        Start-Sleep -Seconds 3
        $process.Refresh()

        if ($process.HasExited) {
            $failures.Add("Process exited after navigating to $($page.Name).")
            break
        }

        if (-not [string]::IsNullOrWhiteSpace($page.Marker)) {
            $markerCondition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                $page.Marker)
            if ($window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $markerCondition).Count -eq 0) {
                $failures.Add("Marker was not found on $($page.Name): $($page.Marker)")
            }
        }

        if ($page.Name -eq 'Master Struktur Folder' -and -not $process.HasExited) {
            $deleteButton = Find-FirstElement -Root $window -Name 'Hapus' -ControlType ([System.Windows.Automation.ControlType]::Button)
            if ($null -eq $deleteButton) {
                $failures.Add('Master Struktur Folder delete button was not found.')
            } else {
                Write-Host 'MASTER_HAPUS_OK'
            }
        }

        if ($page.Name -eq 'Panduan' -and -not $process.HasExited) {
            foreach ($marker in @('Alur Cepat', 'Backup dan Restore')) {
                $guideMarkerCondition = New-Object System.Windows.Automation.PropertyCondition(
                    [System.Windows.Automation.AutomationElement]::NameProperty,
                    $marker)
                if ($window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $guideMarkerCondition).Count -eq 0) {
                    $failures.Add("Panduan marker was not found: $marker")
                } else {
                    Write-Host "PANDUAN_MARKER_OK=$marker"
                }
            }
        }

        if ($page.Name -eq 'Manajemen Pekerjaan' -and -not $process.HasExited) {
            $windowRect = $window.Current.BoundingRectangle
            $openButton = $null
            $buttonCondition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                'Buka')
            foreach ($candidate in $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $buttonCondition)) {
                if ($candidate.Current.ControlType -ne [System.Windows.Automation.ControlType]::Button -or $candidate.Current.IsOffscreen) {
                    continue
                }
                $buttonRect = $candidate.Current.BoundingRectangle
                $centerX = $buttonRect.X + $buttonRect.Width / 2
                $centerY = $buttonRect.Y + $buttonRect.Height / 2
                if ($centerX -ge $windowRect.X -and $centerX -le $windowRect.Right -and
                    $centerY -ge $windowRect.Y -and $centerY -le $windowRect.Bottom) {
                    $openButton = $candidate
                    break
                }
            }
            if ($null -eq $openButton) {
                Write-Host 'PROJECT_DETAIL_SKIP=no visible project'
            } else {
                Click-Element -Element $openButton -Handle $process.MainWindowHandle
                Start-Sleep -Seconds 3
                $process.Refresh()
                if ($process.HasExited) {
                    $failures.Add('Process exited after opening project detail.')
                } else {
                    $refreshMarker = Find-FirstElement -Root $window -Name 'Refresh Dokumen' -ControlType ([System.Windows.Automation.ControlType]::Button)
                    if ($null -eq $refreshMarker) {
                        $failures.Add('Project detail marker was not found: Refresh Dokumen')
                    } else {
                        Write-Host 'PROJECT_DETAIL_MARKER_OK=Refresh Dokumen'
                        $backButton = Find-FirstElement -Root $window -Name 'Kembali' -ControlType ([System.Windows.Automation.ControlType]::Button)
                        if ($null -eq $backButton) {
                            $failures.Add('Project detail back button was not found.')
                        } else {
                            Click-Element -Element $backButton -Handle $process.MainWindowHandle
                            Start-Sleep -Seconds 3
                            $process.Refresh()
                            if ($process.HasExited) {
                                $failures.Add('Process exited after returning from project detail.')
                            } else {
                                $listMarker = Find-FirstElement -Root $window -Name 'Buat Pekerjaan' -ControlType ([System.Windows.Automation.ControlType]::Button)
                                if ($null -eq $listMarker) {
                                    $failures.Add('Project list marker was not found after returning: Buat Pekerjaan')
                                } else {
                                    Write-Host 'PROJECT_DETAIL_OK'
                                }
                            }
                        }
                    }
                }
            }
        }

        Write-Host "NAV_OK=$($page.Name)"
    }

    if (-not $process.HasExited) {
        foreach ($marker in @('Simpan Lokasi Backup', 'Buat Backup Sekarang', 'Siapkan Restore + Restart')) {
            $markerCondition = New-Object System.Windows.Automation.PropertyCondition(
                [System.Windows.Automation.AutomationElement]::NameProperty,
                $marker)
            if ($window.FindAll([System.Windows.Automation.TreeScope]::Descendants, $markerCondition).Count -eq 0) {
                $failures.Add("Pengaturan marker was not found: $marker")
            } else {
                Write-Host "PENGATURAN_MARKER_OK=$marker"
            }
        }
    }

    if (-not $process.HasExited) {
        $restoreButton = Find-FirstElement -Root $window -Name 'Siapkan Restore + Restart' -ControlType ([System.Windows.Automation.ControlType]::Button)
        if ($null -eq $restoreButton) {
            $failures.Add('Pengaturan restore button was not found for scroll check.')
        } else {
            $windowRect = $window.Current.BoundingRectangle
            $beforeRect = $restoreButton.Current.BoundingRectangle
            $scrollBar = $null
            foreach ($element in $window.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
                if ($element.Current.ControlType -eq [System.Windows.Automation.ControlType]::ScrollBar) {
                    $scrollBar = $element
                    break
                }
            }
            if ($null -eq $scrollBar) {
                $failures.Add('Pengaturan scroll bar was not found.')
            } else {
                $range = $null
                if (-not $scrollBar.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern, [ref]$range)) {
                    $failures.Add('Pengaturan scroll bar range pattern was not available.')
                } else {
                    try {
                        $range.SetValue($range.Current.Maximum)
                        Start-Sleep -Milliseconds 600
                        $afterRect = $restoreButton.Current.BoundingRectangle
                        if ($afterRect.Top -ge $windowRect.Top -and $afterRect.Bottom -le $windowRect.Bottom) {
                            Write-Host 'PENGATURAN_SCROLL_OK'
                        } else {
                            $failures.Add("Pengaturan scroll did not reveal restore button: before=$($beforeRect.Top)/$($beforeRect.Bottom), after=$($afterRect.Top)/$($afterRect.Bottom), window=$($windowRect.Top)/$($windowRect.Bottom)")
                        }
                    } catch {
                        $failures.Add("Pengaturan scroll check failed: $($_.Exception.Message)")
                    }
                }
            }
        }
    }

    if (-not $process.HasExited) {
        [SmokeWindow]::MoveWindow($process.MainWindowHandle, 80, 80, 1000, 640, $true) | Out-Null
        Start-Sleep -Seconds 1
        [SmokeWindow]::MoveWindow($process.MainWindowHandle, 80, 80, 1200, 720, $true) | Out-Null
        Start-Sleep -Seconds 1
        $process.Refresh()
        if ($process.HasExited) {
            $failures.Add('Process exited after window resize.')
        } else {
            Write-Host "RESIZE_OK=1000x640,1200x720"
            $workingSetMb = [Math]::Round($process.WorkingSet64 / 1MB, 1)
            Write-Host "WORKING_SET_MB=$workingSetMb"
        }
    }
}

if (-not $process.HasExited) {
    $process.CloseMainWindow() | Out-Null
    Start-Sleep -Seconds 4
    $process.Refresh()
    if (-not $process.HasExited) {
        $process.Kill()
        $process.WaitForExit()
    }
}

$stderr = if (Test-Path -LiteralPath $stderrPath) { Get-Content -LiteralPath $stderrPath -Raw } else { '' }
if (-not [string]::IsNullOrWhiteSpace($stderr)) {
    $failures.Add("STDERR was not empty: $stderr")
}
$wordAfter = @(Get-Process -Name WINWORD -ErrorAction SilentlyContinue | ForEach-Object { $_.Id }) -join ','
Write-Host "WINWORD_BEFORE=$wordBefore"
Write-Host "WINWORD_AFTER=$wordAfter"
Write-Host "EXITED=$($process.HasExited)"

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "FAIL=$failure"
    }
    exit 1
}

Write-Host 'SMOKE_OK'
exit 0
