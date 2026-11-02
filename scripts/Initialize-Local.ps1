param([string]$InfrastructureEnv = (Join-Path $PSScriptRoot '../../fiap-cloud-games-infrastructure/.env'))
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$target = Join-Path $root '.env'
if (Test-Path -LiteralPath $target) { Write-Output '.env existente preservado.'; exit 0 }
if (-not (Test-Path -LiteralPath $InfrastructureEnv)) { throw 'Informe o .env local da Infraestrutura com -InfrastructureEnv.' }
$infra = @{}
Get-Content -LiteralPath $InfrastructureEnv | Where-Object { $_ -match '^[A-Z_]+=' } | ForEach-Object { $parts = $_.Split('=', 2); $infra[$parts[0]] = $parts[1] }
$random = [Security.Cryptography.RandomNumberGenerator]::Create()
function New-LocalSecret {
    $bytes = New-Object byte[] 32
    $random.GetBytes($bytes)
    [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
}
try {
    $lines = @(
        "ConnectionStrings__Users=Host=127.0.0.1;Port=$($infra.POSTGRES_PORT);Database=fcg_users;Username=fcg_users;Password=$($infra.USERS_DB_PASSWORD)",
        "USERS_DOCKER_CONNECTION=Host=postgres;Port=5432;Database=fcg_users;Username=fcg_users;Password=$($infra.USERS_DB_PASSWORD)",
        "Jwt__Key=$(New-LocalSecret)", 'Jwt__Issuer=fiap-cloud-games-users', 'Jwt__Audience=fiap-cloud-games', 'Jwt__ExpirationMinutes=60',
        'RabbitMq__Host=127.0.0.1', "RabbitMq__Port=$($infra.RABBITMQ_PORT)", 'RabbitMq__VirtualHost=fcg', 'RabbitMq__Username=fcg_local', "RabbitMq__Password=$($infra.RABBITMQ_PASSWORD)",
        'AdminBootstrap__Name=Administrador local', 'AdminBootstrap__Email=admin@example.com', "AdminBootstrap__Password=Aa1!$(New-LocalSecret)",
        "TEST_DB_PASSWORD=$(New-LocalSecret)", "TEST_RABBIT_PASSWORD=$(New-LocalSecret)", 'TEST_DB_PORT=55433', 'TEST_RABBIT_PORT=5673',
        'ASPNETCORE_URLS=http://127.0.0.1:5081'
    )
    [IO.File]::WriteAllLines($target, $lines, (New-Object Text.UTF8Encoding($false)))
} finally { $random.Dispose() }
Write-Output '.env local criado sem imprimir credenciais. Não versionar.'
