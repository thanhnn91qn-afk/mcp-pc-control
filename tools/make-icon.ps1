# Sinh host\app.ico + setup\app.ico: nen xanh bo goc + con tro chuot trang.
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$sizes = 16, 24, 32, 48, 64, 128, 256

function New-Frame([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    $m = [single]($s * 0.04); $w = $s - 2 * $m; $r = [single]($s * 0.22)
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc($m, $m, $r, $r, 180, 90); $p.AddArc($m + $w - $r, $m, $r, $r, 270, 90)
    $p.AddArc($m + $w - $r, $m + $w - $r, $r, $r, 0, 90); $p.AddArc($m, $m + $w - $r, $r, $r, 90, 90)
    $p.CloseFigure()
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.PointF]::new(0, 0)), ([System.Drawing.PointF]::new($s, $s)), ([System.Drawing.Color]::FromArgb(255, 37, 99, 235)), ([System.Drawing.Color]::FromArgb(255, 109, 40, 217))
    $g.FillPath($bg, $p)

    # con tro chuot (toa do tren luoi 100)
    $k = $s / 100.0
    $pts = @(@(30,20),@(30,76),@(44,63),@(54,84),@(63,80),@(53,59),@(72,59)) | ForEach-Object { [System.Drawing.PointF]::new([single]($_[0] * $k), [single]($_[1] * $k)) }
    $g.FillPolygon([System.Drawing.Brushes]::White, $pts)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 30, 41, 59)), ([single][Math]::Max(1, $s * 0.035))
    $pen.LineJoin = 'Round'
    $g.DrawPolygon($pen, $pts)
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

$frames = $sizes | ForEach-Object { , (New-Frame $_) }
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $frames[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$d.Length); $bw.Write([uint32]$offset)
    $offset += $d.Length
}
foreach ($d in $frames) { $bw.Write($d) }
$bw.Flush()
foreach ($t in 'host', 'setup') { [System.IO.File]::WriteAllBytes((Join-Path $root "$t\app.ico"), $ms.ToArray()) }
"OK: host\app.ico, setup\app.ico ($($ms.Length) bytes)"
