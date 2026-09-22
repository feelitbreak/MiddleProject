import { appendFileSync, existsSync, readFileSync } from 'node:fs';

/**
 * Turns the ESLint JSON report into a GitHub step summary. Warnings are advisory -- deprecations
 * above all -- so they are surfaced here and in Sonar rather than failing the job.
 */
const REPORT = 'eslint-report.json';

if (!existsSync(REPORT)) {
  console.log(`${REPORT} not found; nothing to summarise.`);
  process.exit(0);
}

const report = JSON.parse(readFileSync(REPORT, 'utf8'));
const warnings = report.flatMap((file) =>
  file.messages
    .filter((message) => message.severity === 1)
    .map((message) => ({ file: file.filePath, ...message })),
);

const lines = [];
if (warnings.length === 0) {
  lines.push('### Lint', '', 'No warnings.', '');
} else {
  lines.push(
    `### Lint: ${warnings.length} warning(s), not blocking`,
    '',
    'Advisory only. These do not fail the build and never block a release or a rollback.',
    '',
    '| Location | Rule | Message |',
    '| --- | --- | --- |',
  );
  for (const warning of warnings) {
    const where = `${warning.file.split(/MeterReadingsUI[/\\]/).pop()}:${warning.line}`;
    const message = warning.message.split('\n')[0].slice(0, 120).replaceAll('|', '\\|');
    lines.push(`| ${where} | ${warning.ruleId ?? ''} | ${message} |`);
  }
  lines.push('');
}

const summary = process.env.GITHUB_STEP_SUMMARY;
if (summary) {
  appendFileSync(summary, lines.join('\n'));
} else {
  console.log(lines.join('\n'));
}
