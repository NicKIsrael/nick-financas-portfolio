param(
    [string]$Source = (Join-Path $PSScriptRoot '..\src\NickFinancas.Desktop\Assets\nick-financas-logo.png'),
    [string]$Destination = (Join-Path $PSScriptRoot '..\src\NickFinancas.Desktop\Assets\nick-financas.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourceImage = [Drawing.Bitmap]::new((Resolve-Path $Source).Path)
try {
    $left = $sourceImage.Width
    $top = $sourceImage.Height
    $right = 0
    $bottom = 0
    for ($y = 0; $y -lt $sourceImage.Height; $y += 2) {
        for ($x = 0; $x -lt $sourceImage.Width; $x += 2) {
            if ($sourceImage.GetPixel($x, $y).A -gt 12) {
                $left = [Math]::Min($left, $x)
                $top = [Math]::Min($top, $y)
                $right = [Math]::Max($right, $x)
                $bottom = [Math]::Max($bottom, $y)
            }
        }
    }
    $crop = [Drawing.Rectangle]::FromLTRB($left, $top, $right + 1, $bottom + 1)
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $frames = [Collections.Generic.List[byte[]]]::new()

    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            try {
                $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
                $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.Clear([Drawing.Color]::Transparent)
                $inset = [Math]::Max(1, [Math]::Round($size * 0.03))
                $radius = [Math]::Max(3, [Math]::Round($size * 0.22))
                $rect = [Drawing.RectangleF]::new($inset, $inset, $size - 2 * $inset, $size - 2 * $inset)
                $path = [Drawing.Drawing2D.GraphicsPath]::new()
                try {
                    $diameter = 2 * $radius
                    $path.AddArc($rect.Left, $rect.Top, $diameter, $diameter, 180, 90)
                    $path.AddArc($rect.Right - $diameter, $rect.Top, $diameter, $diameter, 270, 90)
                    $path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
                    $path.AddArc($rect.Left, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
                    $path.CloseFigure()
                    $background = [Drawing.Drawing2D.LinearGradientBrush]::new($rect, [Drawing.Color]::FromArgb(255, 13, 78, 170), [Drawing.Color]::FromArgb(255, 7, 33, 83), 45)
                    try { $graphics.FillPath($background, $path) } finally { $background.Dispose() }
                }
                finally { $path.Dispose() }

                $coinInset = [Math]::Max(1, [Math]::Round($size * 0.06))
                $destinationRect = [Drawing.Rectangle]::new($coinInset, $coinInset, $size - 2 * $coinInset, $size - 2 * $coinInset)
                $graphics.DrawImage($sourceImage, $destinationRect, $crop, [Drawing.GraphicsUnit]::Pixel)
            }
            finally { $graphics.Dispose() }
            $stream = [IO.MemoryStream]::new()
            try {
                $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
                $frames.Add($stream.ToArray())
            }
            finally { $stream.Dispose() }
        }
        finally { $bitmap.Dispose() }
    }

    $file = [IO.File]::Open($Destination, [IO.FileMode]::Create, [IO.FileAccess]::Write)
    $writer = [IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $sizeByte = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([byte]$sizeByte)
            $writer.Write([byte]$sizeByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frame in $frames) { $writer.Write($frame) }
    }
    finally {
        $writer.Dispose()
        $file.Dispose()
    }
}
finally { $sourceImage.Dispose() }

Write-Host "Icone criado em: $Destination"
