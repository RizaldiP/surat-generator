$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$iconDir = Join-Path $repoRoot 'SuratGenerator.Desktop\Resources\Icons'
$iconPath = Join-Path $iconDir 'SuratGenerator.ico'

function New-IconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $path = $null
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

        $scale = $Size / 256.0
        $docX = [single](44 * $scale)
        $docY = [single](26 * $scale)
        $docW = [single](168 * $scale)
        $docH = [single](204 * $scale)
        $radius = [single](18 * $scale)
        $diameter = [single](2 * $radius)
        $right = [single]($docX + $docW - $diameter)
        $bottom = [single]($docY + $docH - $diameter)

        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $path.AddArc($docX, $docY, $diameter, $diameter, [single]180, [single]90)
        $path.AddArc($right, $docY, $diameter, $diameter, [single]270, [single]90)
        $path.AddArc($right, $bottom, $diameter, $diameter, [single]0, [single]90)
        $path.AddArc($docX, $bottom, $diameter, $diameter, [single]90, [single]90)
        $path.CloseFigure()

        $bodyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 37, 99, 235))
        try {
            $graphics.FillPath($bodyBrush, $path)
        }
        finally {
            $bodyBrush.Dispose()
        }

        $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 255, 255), [single](2 * $scale))
        try {
            $graphics.DrawPath($borderPen, $path)
        }
        finally {
            $borderPen.Dispose()
        }

        $graphics.SetClip($path)
        $foldSize = [single](52 * $scale)
        $foldPoints = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF([single]($docX + $docW - $foldSize), $docY)),
            (New-Object System.Drawing.PointF([single]($docX + $docW), [single]($docY + $foldSize))),
            (New-Object System.Drawing.PointF([single]($docX + $docW - $foldSize), [single]($docY + $foldSize)))
        )
        $foldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 23, 67, 163))
        try {
            $graphics.FillPolygon($foldBrush, $foldPoints)
        }
        finally {
            $foldBrush.Dispose()
        }
        $graphics.ResetClip()

        $lineBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
        try {
            $lineX = [single]($docX + 36 * $scale)
            $lineSpecs = @(
                [pscustomobject]@{ Y = 98; Width = 96; Height = 10 },
                [pscustomobject]@{ Y = 128; Width = 96; Height = 8 },
                [pscustomobject]@{ Y = 158; Width = 72; Height = 8 },
                [pscustomobject]@{ Y = 188; Width = 84; Height = 8 }
            )
            foreach ($spec in $lineSpecs) {
                $lineY = [single]([Math]::Round($spec.Y * $scale))
                $lineW = [single]([Math]::Max(1, [int][Math]::Round($spec.Width * $scale)))
                $lineH = [single]([Math]::Max(1, [int][Math]::Round($spec.Height * $scale)))
                $graphics.FillRectangle($lineBrush, $lineX, $lineY, $lineW, $lineH)
            }
        }
        finally {
            $lineBrush.Dispose()
        }

        return $bitmap
    }
    finally {
        if ($null -ne $path) {
            $path.Dispose()
        }
        $graphics.Dispose()
    }
}

function Get-IcoImageBytes {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [bool]$UsePng
    )

    $stream = New-Object System.IO.MemoryStream
    $writer = $null
    try {
        if ($UsePng) {
            $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            return ,$stream.ToArray()
        }

        $writer = New-Object System.IO.BinaryWriter($stream)
        $width = $Bitmap.Width
        $height = $Bitmap.Height
        $pixelStride = $width * 4
        $pixelBytes = $pixelStride * $height
        $maskStride = [int]([Math]::Floor(($width + 31) / 32) * 4)
        $maskBytes = $maskStride * $height

        $writer.Write([int]40)
        $writer.Write([int]$width)
        $writer.Write([int]($height * 2))
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([int]0)
        $writer.Write([int]$pixelBytes)
        $writer.Write([int]0)
        $writer.Write([int]0)
        $writer.Write([int]0)
        $writer.Write([int]0)

        for ($y = $height - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $width; $x++) {
                $color = $Bitmap.GetPixel($x, $y)
                $writer.Write([byte]$color.B)
                $writer.Write([byte]$color.G)
                $writer.Write([byte]$color.R)
                $writer.Write([byte]$color.A)
            }
        }

        $emptyMask = New-Object 'byte[]' $maskBytes
        $writer.Write($emptyMask)
        $writer.Flush()
        return ,$stream.ToArray()
    }
    finally {
        if ($null -ne $writer) {
            $writer.Dispose()
        }
        $stream.Dispose()
    }
}

$sizes = @(16, 32, 48, 64, 128, 256)
$images = New-Object System.Collections.Generic.List[object]
foreach ($size in $sizes) {
    $bitmap = New-IconBitmap -Size $size
    try {
        $imageBytes = Get-IcoImageBytes -Bitmap $bitmap -UsePng ($size -eq 256)
        $images.Add([pscustomobject]@{ Size = $size; Bytes = $imageBytes })
    }
    finally {
        $bitmap.Dispose()
    }
}

$imageCount = $images.Count
$imageOffset = 6 + (16 * $imageCount)
$output = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($output)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$imageCount)

    foreach ($image in $images) {
        $dimension = if ($image.Size -ge 256) { [byte]0 } else { [byte]$image.Size }
        $writer.Write($dimension)
        $writer.Write($dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Bytes.Length)
        $writer.Write([uint32]$imageOffset)
        $imageOffset += $image.Bytes.Length
    }

    foreach ($image in $images) {
        $writer.Write($image.Bytes)
    }

    $writer.Flush()
    if (-not (Test-Path -LiteralPath $iconDir)) {
        New-Item -ItemType Directory -Path $iconDir -Force | Out-Null
    }
    [System.IO.File]::WriteAllBytes($iconPath, $output.ToArray())
}
finally {
    $writer.Dispose()
    $output.Dispose()
}

$iconFile = Get-Item -LiteralPath $iconPath
Write-Host "Created $iconPath ($($iconFile.Length) bytes)"
