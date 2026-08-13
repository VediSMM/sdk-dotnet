<?php

declare(strict_types=1);

function containsLocalAbsolutePath(string $contents): bool
{
    return preg_match('#/(?:Users|home|work|workspace|app)(?:/|\\\\)#', $contents) === 1
        || preg_match('#(?<![A-Za-z0-9])[A-Za-z]:[\\\\/]#', $contents) === 1;
}

if ($argc !== 3) {
    fwrite(STDERR, "Usage: check_artifact.php <nupkg> <snupkg>\n");
    exit(2);
}
$archive = new ZipArchive();
if ($archive->open($argv[1]) !== true) {
    fwrite(STDERR, "Unable to open NuGet artifact\n");
    exit(1);
}
$required = [
    'VediSMM.nuspec',
    'LICENSE',
    'README.md',
    'lib/net8.0/VediSMM.dll',
    'lib/net8.0/VediSMM.xml',
];
foreach ($required as $entry) {
    if ($archive->locateName($entry) === false) {
        fwrite(STDERR, sprintf("Missing artifact entry: %s\n", $entry));
        exit(1);
    }
}
$xml = $archive->getFromName('lib/net8.0/VediSMM.xml');
foreach ([
    'T:VediSMM.Services.TrackingLinksService',
    'T:VediSMM.Services.TrackingAnalyticsService',
    'T:VediSMM.TrackingSettings',
    'M:VediSMM.CallOptions.WithIdempotencyKey(System.String)',
    'M:VediSMM.CallOptions.WithIfMatch(System.String)',
] as $member) {
    if (!is_string($xml) || !str_contains($xml, $member)) {
        fwrite(STDERR, sprintf("Missing public tracking artifact surface: %s\n", $member));
        exit(1);
    }
}
$forbidden = '/(?:^|\/)(?:\.git|\.github|contract|tests?|TestResults|obj|bin)(?:\/|$)/i';
for ($index = 0; $index < $archive->numFiles; $index++) {
    $name = (string) $archive->getNameIndex($index);
    if (preg_match($forbidden, $name) === 1) {
        fwrite(STDERR, sprintf("Forbidden artifact entry: %s\n", $name));
        exit(1);
    }
    $contents = $archive->getFromIndex($index);
    if (!is_string($contents)) {
        continue;
    }
    if (containsLocalAbsolutePath($contents)
        || str_contains($contents, '-----BEGIN ' . 'PRIVATE KEY-----')) {
        fwrite(STDERR, sprintf("Sensitive artifact content: %s\n", $name));
        exit(1);
    }
}
$count = $archive->numFiles;
$archive->close();

$symbols = new ZipArchive();
if ($symbols->open($argv[2]) !== true || $symbols->locateName('lib/net8.0/VediSMM.pdb') === false) {
    fwrite(STDERR, "Missing or invalid symbol package\n");
    exit(1);
}
for ($index = 0; $index < $symbols->numFiles; $index++) {
    $contents = $symbols->getFromIndex($index);
    if (is_string($contents) && containsLocalAbsolutePath($contents)) {
        fwrite(STDERR, "Local absolute path found in symbol package\n");
        exit(1);
    }
}
$symbols->close();
fwrite(STDOUT, sprintf("NuGet artifact verified: %d entries plus symbol package.\n", $count));
