const test = require('node:test');
const assert = require('node:assert/strict');
const parse = require('../extract-consumption.js');

const fixture = `Your month-to-date consumption
$1,234.56
Internal USD cost across 2 enterprises with consumption
Month-to-date consumption by enterprise
Example Enterprise (EMU)
EMU account
GitHub login demo-emu
$1,200.00 internal cost
1.20% consumed
Monthly limit (not a budget)
$100,000.00
Example Organization
GitHub.com account
GitHub login demo-user
$34.56 internal cost
0.04% consumed
Monthly limit (not a budget)
$100,000.00`;

test('reads both synthetic accounts, dollar amounts, limits and percentages', () => {
  const value = parse(fixture);
  assert.equal(value.ok, true);
  assert.equal(value.total, 1234.56);
  assert.equal(value.accounts.length, 2);
  assert.deepEqual(value.accounts[0], { name: 'Example Enterprise (EMU)', kind: 'EMU account', login: 'demo-emu', cost: 1200, limit: 100000, percent: 1.20 });
  assert.equal(value.accounts[1].cost, 34.56);
  assert.equal(value.accounts[1].percent, 0.04);
});
test('ignores blank lines, CRLF and non-breaking spaces', () => assert.equal(parse(fixture.replace(/\n/g, '\r\n\r\n').replace(/ /g, '\u00a0')).ok, true));
test('does not treat an authentication page as zero consumption', () => assert.equal(parse('Microsoft\nSign in\nNext').ok, false));
test('rejects an account with an absent monthly limit', () => assert.equal(parse(fixture.replace(/Monthly limit \(not a budget\)\n\$100,000\.00$/, '')).ok, false));
test('rejects a partial page while one of the accounts is still loading', () => assert.equal(parse(fixture.slice(0, fixture.indexOf('\nExample Organization'))).ok, false));
test('rejects a total that does not agree with account costs', () => assert.equal(parse(fixture.replace('$1,234.56', '$9,999.00')).reason, 'total-mismatch'));
test('accepts a zero amount without inventing minimum consumption', () => {
  const zero = fixture.replace('$1,234.56', '$0.00').replace('$1,200.00', '$0.00').replace('$34.56', '$0.00').replace('1.20%', '0.00%').replace('0.04%', '0.00%');
  const value = parse(zero); assert.equal(value.ok, true); assert.equal(value.total, 0);
});
test('supports more than two enterprises without hard-coded account names', () => {
  const extra = fixture.replace('$1,234.56', '$1,244.56').replace('across 2', 'across 3') + '\nexample-enterprise\nGitHub.com account\nGitHub login sample-user\n$10.00 internal cost\n1.00% consumed\nMonthly limit (not a budget)\n$1,000.00';
  const value = parse(extra); assert.equal(value.ok, true); assert.equal(value.accounts[2].name, 'example-enterprise');
});
test('reads the new Standard limit label without inventing a dollar limit', () => {
  const value = parse(fixture.replaceAll('$100,000.00', 'Standard'));
  assert.equal(value.ok, true);
  assert.equal(value.total, 1234.56);
  assert.equal(value.accounts.length, 2);
  for (const account of value.accounts) {
    assert.equal(account.limit, null);
    assert.equal(account.limitLabel, 'Standard');
  }
});
test('supports a mixture of numeric and Standard limits', () => {
  const value = parse(fixture.replace('$100,000.00', 'Standard'));
  assert.equal(value.ok, true);
  assert.equal(value.accounts[0].limitLabel, 'Standard');
  assert.equal(value.accounts[1].limit, 100000);
});
test('rejects unknown limit labels and partially loaded Standard accounts', () => {
  assert.equal(parse(fixture.replaceAll('$100,000.00', 'Loading')).ok, false);
  assert.equal(parse(fixture.replaceAll('$100,000.00', 'Standard').replace('1.20% consumed', '')).ok, false);
  assert.equal(parse(fixture.replaceAll('$100,000.00', 'Standard Plus')).ok, false);
});
test('Standard limit pages still reject missing accounts and mismatched totals', () => {
  const standard = fixture.replaceAll('$100,000.00', 'Standard');
  assert.equal(parse(standard.slice(0, standard.indexOf('\nExample Organization'))).reason, 'incomplete-accounts');
  assert.equal(parse(standard.replace('$1,234.56', '$9,999.00')).reason, 'total-mismatch');
});
