$src = "C:\Windows\System32\sru\SRUDB.dat"
$raw = "$env:TEMP\sru_raw.dat"
$rep = "$env:TEMP\sru_rep.dat"
Remove-Item $raw,$rep -ErrorAction SilentlyContinue

& esentutl /y $src /d $raw 2>&1 | Out-Null
Copy-Item $raw $rep -Force
& esentutl /p $rep /o 2>&1 | Out-Null

Write-Host "===== TABLES IN RAW COPY (before repair) ====="
& esentutl /mm $raw 2>&1 | Select-String 'Tbl' | ForEach-Object { $_.Line.Trim() }

Write-Host "`n===== TABLES IN REPAIRED COPY ====="
& esentutl /mm $rep 2>&1 | Select-String 'Tbl' | ForEach-Object { $_.Line.Trim() }

Remove-Item $raw,$rep -ErrorAction SilentlyContinue