/* Read visible consumption content only. No cookies, tokens or hidden application state. */
(function () {
  'use strict';
  const money = '\\$\\s*([\\d,]+(?:\\.\\d{1,2})?)';
  const number = value => Number(String(value).replace(/,/g, ''));
  function parseText(raw) {
    const lines = String(raw || '').replace(/\u00a0/g, ' ').split(/\r?\n/).map(s => s.trim()).filter(Boolean);
    const text = lines.join('\n');
    const totalMatch = text.match(new RegExp('Your month-to-date consumption\\s*' + money, 'i'));
    const section = lines.findIndex(s => /^Month-to-date consumption by enterprise$/i.test(s));
    if (!totalMatch || section < 0) return { ok: false, reason: 'missing-section' };
    const accounts = [];
    for (let i = section + 1; i < lines.length; i++) {
      const loginMatch = lines[i].match(/^GitHub login\s+([a-zA-Z0-9_-]+)\s*$/i);
      if (!loginMatch) continue;
      let previous = i - 1;
      const badge = /^(EMU account|GitHub\.com account)$/i;
      const kind = badge.test(lines[previous] || '') ? lines[previous--] : '';
      const name = lines[previous] || '';
      let end = i + 1;
      while (end < lines.length && !/^GitHub login\s/i.test(lines[end])) end++;
      const content = lines.slice(i + 1, end).join('\n');
      const cost = content.match(new RegExp(money + '\\s*internal cost', 'i'));
      const limit = content.match(new RegExp('Monthly limit\\s*\\(not a budget\\)\\s*' + money, 'i'));
      const standardLimit = !limit && /Monthly limit\s*\(not a budget\)\s*\nStandard(?:\n|$)/i.test(content);
      const percentage = content.match(/([\d,]+(?:\.\d+)?)\s*%\s*consumed/i);
      if (!name || !cost || (!limit && !standardLimit) || !percentage) return { ok: false, reason: 'incomplete-account' };
      const account = { name, login: loginMatch[1], kind: kind || (name.includes('(EMU)') ? 'EMU account' : 'GitHub.com account'), cost: number(cost[1]), limit: limit ? number(limit[1]) : null, percent: number(percentage[1]) };
      if (standardLimit) account.limitLabel = 'Standard';
      accounts.push(account);
    }
    if (!accounts.length) return { ok: false, reason: 'no-accounts' };
    const total = number(totalMatch[1]);
    if (!Number.isFinite(total) || accounts.some(a => !Number.isFinite(a.cost) || (a.limit === null ? a.limitLabel !== 'Standard' : !Number.isFinite(a.limit) || a.limit < 0) || !Number.isFinite(a.percent) || a.cost < 0 || a.percent < 0)) return { ok: false, reason: 'invalid-values' };
    if (new Set(accounts.map(a => a.name + '/' + a.login)).size !== accounts.length) return { ok: false, reason: 'duplicate-accounts' };
    const count = text.match(/across\s+(\d+)\s+enterprises?\s+with consumption/i);
    if (count && Number(count[1]) !== accounts.length) return { ok: false, reason: 'incomplete-accounts' };
    if (Math.abs(total - accounts.reduce((sum, a) => sum + a.cost, 0)) > 0.02 + accounts.length * 0.01) return { ok: false, reason: 'total-mismatch' };
    return { ok: true, total, accounts };
  }
  if (typeof module !== 'undefined' && module.exports) { module.exports = parseText; return; }
  if (location.hostname !== 'copilot.github.microsoft.com') return { ok: false, reason: 'login' };
  return parseText(document.body ? document.body.innerText : '');
})();
