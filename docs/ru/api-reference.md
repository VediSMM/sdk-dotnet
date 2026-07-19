# Справочник API .NET SDK

Все 83 пользовательские операции доступны как асинхронные методы с `Task<ApiResponse>`. Передавайте `CallOptions` и `CancellationToken`; административных методов в SDK нет.

## Система

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.System.GetOpenApiAsync(options, cancellationToken)` | `GET` | `/openapi.json` | — |
| `sdk.System.PingAsync(options, cancellationToken)` | `GET` | `/ping` | — |

## Аутентификация

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Auth.ForgotPasswordAsync(options, cancellationToken)` | `POST` | `/auth/forgot-password` | — |
| `sdk.Auth.LoginAsync(options, cancellationToken)` | `POST` | `/auth/login` | — |
| `sdk.Auth.LogoutAsync(options, cancellationToken)` | `POST` | `/auth/logout` | `profile:read` |
| `sdk.Auth.LogoutAllAsync(options, cancellationToken)` | `POST` | `/auth/logout-all` | `profile:read` |
| `sdk.Auth.RefreshAsync(options, cancellationToken)` | `POST` | `/auth/refresh` | — |
| `sdk.Auth.RegisterAsync(options, cancellationToken)` | `POST` | `/auth/register` | — |
| `sdk.Auth.ResendVerificationAsync(options, cancellationToken)` | `POST` | `/auth/email/resend` | — |
| `sdk.Auth.ResetPasswordAsync(options, cancellationToken)` | `POST` | `/auth/reset-password` | — |
| `sdk.Auth.VerifyEmailAsync(options, cancellationToken)` | `POST` | `/auth/email/verify` | — |

## Профиль

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Profile.ChangePasswordAsync(options, cancellationToken)` | `PUT` | `/me/password` | `profile:write` |
| `sdk.Profile.DeleteMeAsync(options, cancellationToken)` | `DELETE` | `/me` | `profile:write` |
| `sdk.Profile.GetMeAsync(options, cancellationToken)` | `GET` | `/me` | `profile:read` |
| `sdk.Profile.UpdateMeAsync(options, cancellationToken)` | `PATCH` | `/me` | `profile:write` |

## Сессии

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Sessions.GetSessionAsync(options, cancellationToken)` | `GET` | `/sessions/{id}` | `profile:read` |
| `sdk.Sessions.ListSessionsAsync(options, cancellationToken)` | `GET` | `/sessions` | `profile:read` |
| `sdk.Sessions.RevokeSessionAsync(options, cancellationToken)` | `DELETE` | `/sessions/{id}` | `profile:write` |

## Аудит

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Audit.ListAuditEventsAsync(options, cancellationToken)` | `GET` | `/audit-events` | `profile:read` |

## Персональные токены

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.PersonalTokens.CreatePersonalTokenAsync(options, cancellationToken)` | `POST` | `/personal-tokens` | `profile:write` |
| `sdk.PersonalTokens.GetPersonalTokenAsync(options, cancellationToken)` | `GET` | `/personal-tokens/{id}` | `profile:read` |
| `sdk.PersonalTokens.ListPersonalTokensAsync(options, cancellationToken)` | `GET` | `/personal-tokens` | `profile:read` |
| `sdk.PersonalTokens.RevokePersonalTokenAsync(options, cancellationToken)` | `DELETE` | `/personal-tokens/{id}` | `profile:write` |
| `sdk.PersonalTokens.RotatePersonalTokenAsync(options, cancellationToken)` | `POST` | `/personal-tokens/{id}/rotate` | `profile:write` |
| `sdk.PersonalTokens.UpdatePersonalTokenAsync(options, cancellationToken)` | `PATCH` | `/personal-tokens/{id}` | `profile:write` |

## Настройки

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Preferences.CreateContentTemplateAsync(options, cancellationToken)` | `POST` | `/me/templates` | `profile:write` |
| `sdk.Preferences.DeleteContentTemplateAsync(options, cancellationToken)` | `DELETE` | `/me/templates/{id}` | `profile:write` |
| `sdk.Preferences.GetContentTemplateAsync(options, cancellationToken)` | `GET` | `/me/templates/{id}` | `profile:read` |
| `sdk.Preferences.GetSignaturesAsync(options, cancellationToken)` | `GET` | `/me/signatures` | `profile:read` |
| `sdk.Preferences.ListContentTemplatesAsync(options, cancellationToken)` | `GET` | `/me/templates` | `profile:read` |
| `sdk.Preferences.ReplaceSignaturesAsync(options, cancellationToken)` | `PUT` | `/me/signatures` | `profile:write` |
| `sdk.Preferences.UpdateContentTemplateAsync(options, cancellationToken)` | `PATCH` | `/me/templates/{id}` | `profile:write` |

## Социальные сети

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Networks.GetNetworkAsync(options, cancellationToken)` | `GET` | `/networks/{key}` | `networks:read` |
| `sdk.Networks.ListNetworksAsync(options, cancellationToken)` | `GET` | `/networks` | `networks:read` |

## Подключения

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Connections.CancelAccountConnectionAsync(options, cancellationToken)` | `DELETE` | `/connection-sessions/{id}` | `accounts:write` |
| `sdk.Connections.ConfirmAccountConnectionAsync(options, cancellationToken)` | `POST` | `/connection-sessions/{id}/confirm` | `accounts:write` |
| `sdk.Connections.GetAccountConnectionAsync(options, cancellationToken)` | `GET` | `/connection-sessions/{id}` | `accounts:read` |
| `sdk.Connections.StartAccountConnectionAsync(options, cancellationToken)` | `POST` | `/connection-sessions` | `accounts:write` |

## Аккаунты

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Accounts.DisconnectAccountAsync(options, cancellationToken)` | `DELETE` | `/accounts/{id}` | `accounts:write` |
| `sdk.Accounts.GetAccountAsync(options, cancellationToken)` | `GET` | `/accounts/{id}` | `accounts:read` |
| `sdk.Accounts.ListAccountsAsync(options, cancellationToken)` | `GET` | `/accounts` | `accounts:read` |
| `sdk.Accounts.VerifyAccountAsync(options, cancellationToken)` | `POST` | `/accounts/{id}/verify` | `accounts:write` |

## Группы

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Groups.CreateGroupAsync(options, cancellationToken)` | `POST` | `/groups` | `groups:write` |
| `sdk.Groups.DeleteGroupAsync(options, cancellationToken)` | `DELETE` | `/groups/{id}` | `groups:write` |
| `sdk.Groups.GetGroupAsync(options, cancellationToken)` | `GET` | `/groups/{id}` | `groups:read` |
| `sdk.Groups.ListGroupsAsync(options, cancellationToken)` | `GET` | `/groups` | `groups:read` |
| `sdk.Groups.ReplaceGroupAccountsAsync(options, cancellationToken)` | `PUT` | `/groups/{id}/accounts` | `groups:write` |
| `sdk.Groups.UpdateGroupAsync(options, cancellationToken)` | `PATCH` | `/groups/{id}` | `groups:write` |

## Медиа

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Media.DeleteMediaAsync(options, cancellationToken)` | `DELETE` | `/media/{id}` | `media:write` |
| `sdk.Media.GetMediaAsync(options, cancellationToken)` | `GET` | `/media/{id}` | `media:read` |
| `sdk.Media.GetMediaContentAsync(options, cancellationToken)` | `GET` | `/media/{id}/content` | `media:read` |
| `sdk.Media.GetSignedMediaContentAsync(options, cancellationToken)` | `GET` | `/media-content` | — |
| `sdk.Media.ListMediaAsync(options, cancellationToken)` | `GET` | `/media` | `media:read` |
| `sdk.Media.UploadMediaAsync(options, cancellationToken)` | `POST` | `/media` | `media:write` |

## Публикации

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Posts.CheckPostConstraintsAsync(options, cancellationToken)` | `POST` | `/posts/constraints` | `posts:read` |
| `sdk.Posts.CreatePostDraftAsync(options, cancellationToken)` | `POST` | `/posts` | `posts:write` |
| `sdk.Posts.DeletePostDraftAsync(options, cancellationToken)` | `DELETE` | `/posts/{id}` | `posts:write` |
| `sdk.Posts.GetPostAsync(options, cancellationToken)` | `GET` | `/posts/{id}` | `posts:read` |
| `sdk.Posts.ListPostsAsync(options, cancellationToken)` | `GET` | `/posts` | `posts:read` |
| `sdk.Posts.SchedulePostAsync(options, cancellationToken)` | `POST` | `/posts/{id}/schedule` | `posts:publish` |
| `sdk.Posts.UnschedulePostAsync(options, cancellationToken)` | `POST` | `/posts/{id}/unschedule` | `posts:publish` |
| `sdk.Posts.UpdatePostDraftAsync(options, cancellationToken)` | `PATCH` | `/posts/{id}` | `posts:write` |

## Задания публикации

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Jobs.DeletePostEverywhereAsync(options, cancellationToken)` | `POST` | `/posts/{id}/delete-everywhere` | `posts:publish` |
| `sdk.Jobs.GetPublicationJobAsync(options, cancellationToken)` | `GET` | `/jobs/{job_id}` | `jobs:read` |
| `sdk.Jobs.ListPublicationJobsAsync(options, cancellationToken)` | `GET` | `/jobs` | `jobs:read` |
| `sdk.Jobs.PublishPostAsync(options, cancellationToken)` | `POST` | `/posts/{id}/publish` | `posts:publish` |
| `sdk.Jobs.RetryPostTargetsAsync(options, cancellationToken)` | `POST` | `/posts/{id}/retry` | `posts:publish` |

## Календарь

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Calendar.ListCalendarEventsAsync(options, cancellationToken)` | `GET` | `/calendar` | `calendar:read` |

## Аналитика

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Analytics.GetAnalyticsAudienceAsync(options, cancellationToken)` | `GET` | `/analytics/audience` | `analytics:read` |
| `sdk.Analytics.GetAnalyticsNetworksAsync(options, cancellationToken)` | `GET` | `/analytics/networks` | `analytics:read` |
| `sdk.Analytics.GetAnalyticsSummaryAsync(options, cancellationToken)` | `GET` | `/analytics/summary` | `analytics:read` |
| `sdk.Analytics.GetAnalyticsTimeseriesAsync(options, cancellationToken)` | `GET` | `/analytics/timeseries` | `analytics:read` |
| `sdk.Analytics.ListAnalyticsPostsAsync(options, cancellationToken)` | `GET` | `/analytics/posts` | `analytics:read` |

## Вебхуки

| Метод SDK | HTTP | Путь | Права |
| --- | --- | --- | --- |
| `sdk.Webhooks.CreateWebhookAsync(options, cancellationToken)` | `POST` | `/webhooks` | `webhooks:write` |
| `sdk.Webhooks.DeleteWebhookAsync(options, cancellationToken)` | `DELETE` | `/webhooks/{webhook_id}` | `webhooks:write` |
| `sdk.Webhooks.GetWebhookAsync(options, cancellationToken)` | `GET` | `/webhooks/{webhook_id}` | `webhooks:read` |
| `sdk.Webhooks.GetWebhookDeliveryAsync(options, cancellationToken)` | `GET` | `/webhooks/{webhook_id}/deliveries/{delivery_id}` | `webhooks:read` |
| `sdk.Webhooks.ListWebhookDeliveriesAsync(options, cancellationToken)` | `GET` | `/webhooks/{webhook_id}/deliveries` | `webhooks:read` |
| `sdk.Webhooks.ListWebhooksAsync(options, cancellationToken)` | `GET` | `/webhooks` | `webhooks:read` |
| `sdk.Webhooks.RetryWebhookDeliveryAsync(options, cancellationToken)` | `POST` | `/webhooks/{webhook_id}/deliveries/{delivery_id}/retry` | `webhooks:write` |
| `sdk.Webhooks.RotateWebhookSecretAsync(options, cancellationToken)` | `POST` | `/webhooks/{webhook_id}/rotate-secret` | `webhooks:write` |
| `sdk.Webhooks.TestWebhookAsync(options, cancellationToken)` | `POST` | `/webhooks/{webhook_id}/test` | `webhooks:write` |
| `sdk.Webhooks.UpdateWebhookAsync(options, cancellationToken)` | `PATCH` | `/webhooks/{webhook_id}` | `webhooks:write` |
