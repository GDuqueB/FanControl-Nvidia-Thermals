$output = 'C:\Users\DEEP\Documents\HWMonitor\artifacts\boot-security.txt'
"SecureBoot=$((Confirm-SecureBootUEFI -ErrorAction SilentlyContinue))" | Set-Content -LiteralPath $output
"BCD:" | Add-Content -LiteralPath $output
bcdedit.exe /enum '{current}' | Add-Content -LiteralPath $output
