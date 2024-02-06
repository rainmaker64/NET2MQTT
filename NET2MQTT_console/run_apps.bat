del /f /q /a .\NetGent_V\bin\Debug\net6.0\logs\2024\01\*.*
del /f /q /a .\NetGent_F\bin\Debug\net6.0\logs\2024\01\*.*

start call .\NetGent_F\bin\Debug\net6.0\NetGent_F.exe
start call .\NetGent_V\bin\Debug\net6.0\NetGent_V.exe

