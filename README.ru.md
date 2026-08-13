# VediSMM SDK для .NET

Нативный клиент для .NET 8+ ко всем 94 операциям публичного пользовательского API VediSMM v1. В SDK есть безопасный переиспользуемый HTTP-транспорт, `Task`/`CancellationToken`, типизированные ошибки, курсорная пагинация, конечное ожидание заданий, потоковая передача, идемпотентность и проверка вебхуков по исходным байтам. Административных методов намеренно нет.

[English README](README.md) · [Руководство](docs/ru/guide.md) · [Справочник API](docs/ru/api-reference.md) · [Документация API VediSMM](https://vedismm.ru/docs/api)

## Требования

- .NET 8 или .NET 10 SDK

## Установка из неизменяемого тега GitHub

До настройки доверенной публикации в NuGet соберите точный тег в локальный каталог пакетов:

```bash
git clone --branch v0.1.1 --depth 1 https://github.com/VediSMM/sdk-dotnet.git
cd sdk-dotnet
dotnet pack src/VediSMM/VediSMM.csproj --configuration Release --output ./artifacts
```

Подключите нативный пакет в своём проекте:

```bash
dotnet add package VediSMM --version 0.1.1 --source ../sdk-dotnet/artifacts
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

Фасад содержит специализированные сервисы `TrackingLinks` и `TrackingAnalytics`:

```csharp
ApiResponse created = await sdk.TrackingLinks.CreateAsync(
    new Dictionary<string, object?>
    {
        ["destination_url"] = "https://example.com/article?a=1#part",
    },
    CallOptions.WithIdempotencyKey("create-link-1"),
    cancellationToken);
ApiResponse summary = await sdk.TrackingAnalytics.SummaryAsync(
    new Dictionary<string, object?>
    {
        ["from"] = "2026-08-01",
        ["to"] = "2026-08-13",
    },
    cancellationToken: cancellationToken);
```

Destination передаётся неизменённой строкой. Проверка, сокращение, атрибуция и redirect принадлежат серверу. Метода обновления destination намеренно нет: для другого адреса создайте новую ссылку. Отключение и архивация требуют `CallOptions.WithIfMatch(...)` до любого HTTP I/O. В тела создания/обновления публикации можно явно вложить `TrackingSettings`; SDK не добавляет настройки трекинга и не переписывает URL публикации.

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

Подробности о настройках трекинга, аналитике, ошибках, пагинации, медиа, заданиях и вебхуках — в [руководстве](docs/ru/guide.md).

## Контракт

- версия контракта API: `1.1.0`
- SHA-256 контракта: `08e7b140c93a1ef16aa54de509799388e8b01da36b93e47e9733a36e57e40f8a`
- пользовательские операции: `94/94`
- лицензия: MIT
