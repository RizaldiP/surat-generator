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
    public static void Wheel(IntPtr hWnd, int x, int y, int delta) {
        SetForegroundWindow(hWnd);
        SetCursorPos(x, y);
        mouse_event(0x0800, 0, 0, unchecked((uint)delta), IntPtr.Zero);
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

function Find-ElementByAutomationId {
    param(
        [System.Windows.Automation.AutomationElement] $Root,
        [string] $AutomationId
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId)
    $matches = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
    if ($matches.Count -gt 0) {
        return $matches[0]
    }
    return $null
}

function Test-ScrollToMarker {
    param(
        [System.Windows.Automation.AutomationElement] $Window,
        [IntPtr] $Handle,
        [string] $PageName,
        [string] $ScrollViewerId,
        [string] $Marker,
        [System.Windows.Automation.ControlType] $MarkerControlType = [System.Windows.Automation.ControlType]::Text,
        [int] $WheelSteps = 20,
        [double] $WheelXRatio = 0.5,
        [int] $WheelDelta = -120
    )

    $scrollViewer = Find-ElementByAutomationId -Root $Window -AutomationId $ScrollViewerId
    if ($null -eq $scrollViewer) {
        $failures.Add("$PageName scroll viewer was not found: $ScrollViewerId")
        return
    }

    foreach ($element in $scrollViewer.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)) {
        if ($element.Current.ControlType -ne [System.Windows.Automation.ControlType]::ScrollBar) {
            continue
        }
        $range = $null
        if ($element.TryGetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern, [ref]$range)) {
            try {
                $range.SetValue($range.Current.Minimum)
            } catch {
            }
        }
        break
    }

    $scrollRect = $scrollViewer.Current.BoundingRectangle
    $scrollX = [int]($scrollRect.X + $scrollRect.Width * $WheelXRatio)
    $scrollY = [int]($scrollRect.Y + $scrollRect.Height / 2)
    for ($step = 0; $step -lt $WheelSteps; $step++) {
        [SmokeWindow]::Wheel($Handle, $scrollX, $scrollY, $WheelDelta)
        Start-Sleep -Milliseconds 120
    }
    Start-Sleep -Milliseconds 600

    $markerElement = Find-FirstElement -Root $Window -Name $Marker -ControlType $MarkerControlType
    if ($null -eq $markerElement) {
        $failures.Add("$PageName marker was not found after scrolling: $Marker")
        return
    }

    $windowRect = $Window.Current.BoundingRectangle
    $markerRect = $markerElement.Current.BoundingRectangle
    if ($markerRect.Top -ge $windowRect.Top -and $markerRect.Bottom -le $windowRect.Bottom) {
        Write-Host "$($PageName.ToUpperInvariant())_SCROLL_OK"
    } else {
        $failures.Add("$PageName scroll did not reveal marker '$Marker': marker=$($markerRect.Top)/$($markerRect.Bottom), window=$($windowRect.Top)/$($windowRect.Bottom)")
    }
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
            Test-ScrollToMarker -Window $window -Handle $process.MainWindowHandle -PageName 'Panduan' -ScrollViewerId 'GuideScrollViewer' -Marker 'Backup dan Restore' -WheelSteps 24
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
                        Test-ScrollToMarker -Window $window -Handle $process.MainWindowHandle -PageName 'ProjectDetail' -ScrollViewerId 'ProjectDetailScrollViewer' -Marker 'Field Profil' -WheelSteps 32 -WheelXRatio 0.01
                        Test-ScrollToMarker -Window $window -Handle $process.MainWindowHandle -PageName 'ProjectDetailBack' -ScrollViewerId 'ProjectDetailScrollViewer' -Marker 'Kembali' -MarkerControlType ([System.Windows.Automation.ControlType]::Button) -WheelSteps 1 -WheelXRatio 0.01
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
        Test-ScrollToMarker -Window $window -Handle $process.MainWindowHandle -PageName 'Pengaturan' -ScrollViewerId 'SettingsScrollViewer' -Marker 'Siapkan Restore + Restart' -MarkerControlType ([System.Windows.Automation.ControlType]::Button) -WheelSteps 24
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
