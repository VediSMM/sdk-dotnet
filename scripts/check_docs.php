<?php

declare(strict_types=1);

$root = dirname(__DIR__);
$required = [
    'README.md',
    'README.ru.md',
    'docs/en/guide.md',
    'docs/ru/guide.md',
    'docs/en/api-reference.md',
    'docs/ru/api-reference.md',
    'examples/VediSMM.Quickstart/Program.cs',
    'CHANGELOG.md',
    'SECURITY.md',
    'CONTRIBUTING.md',
    'LICENSE',
];

foreach ($required as $file) {
    $contents = file_get_contents($root . '/' . $file);
    if (!is_string($contents) || trim($contents) === '') {
        fwrite(STDERR, sprintf("Missing or empty documentation file: %s\n", $file));
        exit(1);
    }
    if (preg_match('/\b(?:TBD|TODO|FIXME)\b/i', $contents) === 1) {
        fwrite(STDERR, sprintf("Placeholder found in %s\n", $file));
        exit(1);
    }
}

$manifest = json_decode(
    (string) file_get_contents($root . '/contract/sdk-operations.json'),
    true,
    flags: JSON_THROW_ON_ERROR,
);
$english = (string) file_get_contents($root . '/docs/en/api-reference.md');
$russian = (string) file_get_contents($root . '/docs/ru/api-reference.md');
$seen = [];
$focused = [
    'archiveTrackingLink' => '.ArchiveAsync(id, options, cancellationToken)',
    'createTrackingLink' => '.CreateAsync(request, options, cancellationToken)',
    'disableTrackingLink' => '.DisableAsync(id, options, cancellationToken)',
    'getTrackingLink' => '.GetAsync(id, options, cancellationToken)',
    'listTrackingLinks' => '.ListAsync(query, options, cancellationToken)',
    'getTrackingAnalyticsGeo' => '.GeoAsync(filter, options, cancellationToken)',
    'getTrackingAnalyticsSummary' => '.SummaryAsync(filter, options, cancellationToken)',
    'getTrackingAnalyticsTimeseries' => '.TimeseriesAsync(filter, options, cancellationToken)',
    'listTrackingAnalyticsLinks' => '.LinksAsync(filter, options, cancellationToken)',
    'listTrackingAnalyticsPosts' => '.PostsAsync(filter, options, cancellationToken)',
    'listTrackingAnalyticsSources' => '.SourcesAsync(filter, options, cancellationToken)',
];
foreach ($manifest['operations'] ?? [] as $operation) {
    $id = (string) ($operation['operation_id'] ?? '');
    if ($id === '' || isset($seen[$id])) {
        fwrite(STDERR, "Invalid operation ownership in manifest\n");
        exit(1);
    }
    $seen[$id] = true;
    $method = strtoupper($id[0]) . substr($id, 1);
    $needle = $focused[$id] ?? ('.' . $method . 'Async(options, cancellationToken)');
    if (!str_contains($english, $needle) || !str_contains($russian, $needle)) {
        fwrite(STDERR, sprintf("Missing API reference entry: %s\n", $id));
        exit(1);
    }
}
if (count($seen) !== 94) {
    fwrite(STDERR, sprintf("Documentation coverage must be 94/94, got %d\n", count($seen)));
    exit(1);
}

$requiredTrackingText = [
    'README.md' => ['TrackingSettings', 'destination', 'Idempotency-Key', 'If-Match'],
    'README.ru.md' => ['TrackingSettings', 'destination', 'Idempotency-Key', 'If-Match'],
    'docs/en/guide.md' => ['TrackingLinks', 'TrackingAnalytics', 'FeatureCollection', 'shorten_links'],
    'docs/ru/guide.md' => ['TrackingLinks', 'TrackingAnalytics', 'FeatureCollection', 'shorten_links'],
];
foreach ($requiredTrackingText as $file => $needles) {
    $contents = (string) file_get_contents($root . '/' . $file);
    foreach ($needles as $needle) {
        if (!str_contains($contents, $needle)) {
            fwrite(STDERR, sprintf("Missing tracking documentation in %s: %s\n", $file, $needle));
            exit(1);
        }
    }
}

fwrite(STDOUT, "Documentation covers all 94 operations and tracking semantics in English and Russian.\n");
