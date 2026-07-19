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
foreach ($manifest['operations'] ?? [] as $operation) {
    $id = (string) ($operation['operation_id'] ?? '');
    if ($id === '' || isset($seen[$id])) {
        fwrite(STDERR, "Invalid operation ownership in manifest\n");
        exit(1);
    }
    $seen[$id] = true;
    $method = strtoupper($id[0]) . substr($id, 1);
    $needle = '.' . $method . 'Async(options, cancellationToken)`';
    if (!str_contains($english, $needle) || !str_contains($russian, $needle)) {
        fwrite(STDERR, sprintf("Missing API reference entry: %s\n", $id));
        exit(1);
    }
}
if (count($seen) !== 83) {
    fwrite(STDERR, sprintf("Documentation coverage must be 83/83, got %d\n", count($seen)));
    exit(1);
}

fwrite(STDOUT, "Documentation covers all 83 operations in English and Russian.\n");
