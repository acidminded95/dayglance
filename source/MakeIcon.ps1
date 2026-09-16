Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 64,64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$disc = New-Object System.Drawing.SolidBrush ([System.Drawing.ColorTranslator]::FromHtml('#A4E9CC'))
$graphics.FillEllipse($disc,2,2,60,60)
$pen = New-Object System.Drawing.Pen ([System.Drawing.ColorTranslator]::FromHtml('#11151D')),6
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round; $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round; $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
$graphics.DrawLines($pen,[System.Drawing.PointF[]]@((New-Object System.Drawing.PointF 32,15),(New-Object System.Drawing.PointF 32,32),(New-Object System.Drawing.PointF 44.5,39.5)))
$stream = New-Object System.IO.MemoryStream
$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
$bytes = $stream.ToArray()
$output = [System.IO.File]::Create((Join-Path $PSScriptRoot 'Dayglance.ico'))
$writer = New-Object System.IO.BinaryWriter $output
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]64); $writer.Write([byte]64); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22)
$writer.Write($bytes)
$writer.Dispose(); $stream.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
