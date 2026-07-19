<?php

declare(strict_types=1);

if ($argc !== 2) {
    fwrite(STDERR, "Usage: check_coverage.php <coverage.cobertura.xml>\n");
    exit(2);
}
$contents = file_get_contents($argv[1]);
if (!is_string($contents)
    || !str_contains($contents, '<class ')
    || preg_match('/<coverage\b[^>]*\bline-rate="([0-9.]+)"[^>]*\bbranch-rate="([0-9.]+)"/D', $contents, $matches) !== 1) {
    fwrite(STDERR, "Unable to read Cobertura coverage totals\n");
    exit(1);
}
$line = (float) $matches[1];
$branch = (float) $matches[2];
if ($line < 0.75 || $branch < 0.55) {
    fwrite(STDERR, sprintf(
        "Coverage below threshold: line %.2f%% (75%% required), branch %.2f%% (55%% required)\n",
        $line * 100,
        $branch * 100,
    ));
    exit(1);
}
fwrite(STDOUT, sprintf("Coverage verified: line %.2f%%, branch %.2f%%.\n", $line * 100, $branch * 100));
