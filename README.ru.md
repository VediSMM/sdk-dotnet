# VediSMM SDK для .NET

Нативный клиент для .NET 8+ ко всем 83 операциям публичного пользовательского API VediSMM v1. В SDK есть безопасный переиспользуемый HTTP-транспорт, `Task`/`CancellationToken`, типизированные ошибки, курсорная пагинация, конечное ожидание заданий, потоковая передача, идемпотентность и проверка вебхуков по исходным байтам. Административных методов намеренно нет.

[English README](README.md) · [Руководство](docs/ru/guide.md) · [Справочник API](docs/ru/api-reference.md) · [Документация API VediSMM](https://vedismm.ru/docs/api)

## Требования

- .NET 8 или .NET 10 SDK

## Установка из неизменяемого тега GitHub

До настройки доверенной публикации в NuGet соберите точный тег в локальный каталог пакетов:

```bash
git clone --branch v0.1.0 --depth 1 https://github.com/VediSMM/sdk-dotnet.git
cd sdk-dotnet
dotnet pack src/VediSMM/VediSMM.csproj --configuration Release --output ./artifacts
```

Подключите нативный пакет в своём проекте:

```bash
dotnet add package VediSMM --version 0.1.0 --source ../sdk-dotnet/artifacts
```

## Быстрый старт

```csharp
using VediSMM;

using var sdk = new VediSmm(new VediSmmClientOptions
{
    AccessToken = Environment.GetEnvironmentVariable("VEDISMM_ACCESS_TOKEN"),
});

ApiResponse response = await sdk.Accounts.ListAccountsAsync(
    cancellationToken: cancellationToken);
Console.WriteLine(response.Data);
```

Переиспользуйте один экземпляр `VediSmm` на протяжении жизни приложения. Фасад, созданный из настроек, владеет безопасным пулом соединений и освобождает его. Фасад, созданный поверх готового `VediSmmClient`, только заимствует клиент и не уничтожает его.

## Безопасность по умолчанию

- рабочий адрес — `https://vedismm.ru/api/v1/`;
- перенаправления запрещены, поэтому Bearer-токен не уйдёт на другой origin;
- токены остаются в памяти и удаляются из ошибок и диагностических строк;
- получение токена, HTTP-запрос, повторы и чтение тела используют единый конечный дедлайн;
- успешные ответы и ошибки имеют жёсткие ограничения размера;
- повторы ограничены идемпотентными или явно идемпотентными вызовами;
- сохраняются `Retry-After`, `Idempotency-Key`, `If-Match`, ID запроса и ETag;
- загрузка может создавать новый поток на каждую попытку, скачивание поддерживает потоковую запись;
- вебхуки проверяются по точным исходным байтам с HMAC-SHA256, сравнением за постоянное время, допуском времени и защитой от повторов.

Подробности об ошибках, пагинации, медиа, заданиях и вебхуках — в [руководстве](docs/ru/guide.md).

## Контракт

- версия контракта API: `1.0.0`
- SHA-256 контракта: `0318da9e05a622860cb2cf154c6bca50e931349b3e7a8df54d76173ad961c521`
- пользовательские операции: `83/83`
- лицензия: MIT
