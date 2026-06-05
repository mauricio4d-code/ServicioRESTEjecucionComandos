$content = Get-Content 'Product.wxs' -Raw
# Remove all Bitmap control lines (entire line containing Type="Bitmap")
$content = ($content -split "`r?`n" | Where-Object { $_ -notmatch 'Type="Bitmap"' }) -join "`r`n"
# Remove Text="" from Icon control
$content = $content -replace '<Control Id="Icon" Type="Icon" X="20" Y="20" Width="20" Height="20" FixedSize="yes" IconSize="32" Text="" />', '<Control Id="Icon" Type="Icon" X="20" Y="20" Width="20" Height="20" FixedSize="yes" IconSize="32" />'
Set-Content 'Product.wxs' -Value $content -NoNewline -Encoding UTF8