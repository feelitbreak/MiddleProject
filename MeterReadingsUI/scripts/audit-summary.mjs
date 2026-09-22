import { appendFileSync, existsSync, readFileSync } from 'node:fs';

/**
 * Summarises `npm audit --json` for a GitHub step summary.
 *
 * Reports only. An advisory is published on someone else's schedule, so a new one can appear
 * against a commit nobody has touched; failing the build on that would stop a release, or a
 * rollback to a tag that was fine yesterday.
 */
const SEVERITIES = ['critical', 'high', 'moderate', 'low', 'info'];

const reportPath = process.argv[2] ?? 'audit-report.json';

if (!existsSync(reportPath)) {
  console.log(`${reportPath} not found; nothing to summarise.`);
  process.exit(0);
}

const raw = readFileSync(reportPath, 'utf8').trim();
if (raw === '') {
  console.log('Empty audit report; npm produced no output.');
  process.exit(0);
}

const report = JSON.parse(raw);
const counts = report.metadata?.vulnerabilities ?? {};
const lines = ['### Dependency audit', ''];

if ((counts.total ?? 0) === 0) {
  lines.push('No known advisories.', '');
} else {
  const present = SEVERITIES.filter((severity) => (counts[severity] ?? 0) > 0);
  lines.push(
    present.map((severity) => `**${counts[severity]} ${severity}**`).join(' · '),
    '',
    'Advisory only. Triage with `npm audit`; `npm audit --omit=dev` narrows it to what ships.',
    '',
    '| Package | Severity | Vulnerable range | Fix available |',
    '| --- | --- | --- | --- |',
  );

  const entries = Object.values(report.vulnerabilities ?? {});
  entries.sort((a, b) => SEVERITIES.indexOf(a.severity) - SEVERITIES.indexOf(b.severity));
  for (const entry of entries) {
    const fix =
      entry.fixAvailable === true
        ? 'yes'
        : entry.fixAvailable
          ? `${entry.fixAvailable.name}@${entry.fixAvailable.version}${entry.fixAvailable.isSemVerMajor ? ' (major)' : ''}`
          : 'no';
    lines.push(`| ${entry.name} | ${entry.severity} | ${entry.range} | ${fix} |`);
  }
  lines.push('');
}

const summary = process.env.GITHUB_STEP_SUMMARY;
if (summary) {
  appendFileSync(summary, lines.join('\n'));
} else {
  console.log(lines.join('\n'));
}
