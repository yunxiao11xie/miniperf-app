Add-Type -AssemblyName System.Drawing
$size = 32
$bmp = New-Object System.Drawing.Bitmap($size, $size)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
function NewRounded([single]$x, [single]$y, [single]$w, [single]$h, [single]$r) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath
  $p.AddArc($x, $y, $r*2, $r*2, 180, 90)
  $p.AddArc($x+$w-$r*2, $y, $r*2, $r*2, 270, 90)
  $p.AddArc($x+$w-$r*2, $y+$h-$r*2, $r*2, $r*2, 0, 90)
  $p.AddArc($x, $y+$h-$r*2, $r*2, $r*2, 90, 90)
  $p.CloseFigure()
  return $p
}
$chip = NewRounded 1 1 30 30 8
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush((New-Object System.Drawing.Point(0,0)), (New-Object System.Drawing.Point(32,32)), [System.Drawing.Color]::FromArgb(255,37,99,235), [System.Drawing.Color]::FromArgb(255,124,58,237))
$g.FillPath($grad, $chip)
$font = New-Object System.Drawing.Font('Segoe UI', 19, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$g.DrawString('M', $font, $white, (New-Object System.Drawing.RectangleF(0, -1, 32, 32)), $sf)
$g.Dispose()

# --- build classic BMP-in-ICO (32bpp + AND mask) ---
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)
# BITMAPINFOHEADER
$bw.Write([uint32]40)      # biSize
$bw.Write([int32]$size)    # biWidth
$bw.Write([int32]($size*2))# biHeight (XOR + AND)
$bw.Write([uint16]1)       # planes
$bw.Write([uint16]32)      # bpp
$bw.Write([uint32]0)       # compression
$bw.Write([uint32](40 + $size*$size*4 + ($size/8)*$size))
$bw.Write([int32]0); $bw.Write([int32]0)
$bw.Write([uint32]0); $bw.Write([uint32]0)
# pixels bottom-up BGRA
for ($y = $size - 1; $y -ge 0; $y--) {
  for ($x = 0; $x -lt $size; $x++) {
    $p = $bmp.GetPixel($x, $y)
    $bw.Write([byte]$p.B); $bw.Write([byte]$p.G); $bw.Write([byte]$p.R); $bw.Write([byte]$p.A)
  }
}
# AND mask (all zero = use alpha)
for ($i = 0; $i -lt ($size/8)*$size; $i++) { $bw.Write([byte]0) }
$bw.Flush()
$imgData = $ms.ToArray()

$out = New-Object System.IO.MemoryStream
$ow = New-Object System.IO.BinaryWriter($out)
$ow.Write([uint16]0); $ow.Write([uint16]1); $ow.Write([uint16]1)   # ICONDIR
$ow.Write([byte]$size); $ow.Write([byte]$size); $ow.Write([byte]0); $ow.Write([byte]0)
$ow.Write([uint16]1); $ow.Write([uint16]32)
$ow.Write([uint32]$imgData.Length); $ow.Write([uint32]22)
$ow.Write($imgData)
$ow.Flush()
[IO.File]::WriteAllBytes("$PSScriptRoot\app.ico", $out.ToArray())
Write-Host ("app.ico (BMP format) " + $out.Length + " bytes")
