// Live diagnostics: pipes the buffer to `ns check <file>` and turns `path:line[:col]: error: msg` into a squiggle.
// Hovers: stdlib signatures and docs from `ns doc` (one `signature<TAB>doc` line per overload), loaded once;
// variable types from `ns types` (`line<TAB>col<TAB>name<TAB>type`; col 0 = assigned on that line), per buffer version.
const vscode = require('vscode');
const { execFile } = require('child_process');

function activate(context) {
  const diags = vscode.languages.createDiagnosticCollection('ns');
  const timers = new Map();

  const check = (doc) => {
    if (doc.languageId !== 'ns') return;
    const exe = vscode.workspace.getConfiguration('ns').get('path') || 'ns';
    const version = doc.version;
    const p = execFile(exe, ['check', doc.fileName], { timeout: 10000 }, (err, _out, stderr) => {
      if (doc.version !== version) return; // stale
      const m = /^.*?:(\d+)(?::(\d+))?: error: (.*)$/m.exec(stderr || '');
      if (!m) {
        diags.set(doc.uri, []);
        if (err && err.code === 'ENOENT') vscode.window.showWarningMessage(`NS: '${exe}' not found; set ns.path`);
        return;
      }
      const line = Math.min(Math.max(+m[1] - 1, 0), doc.lineCount - 1);
      const text = doc.lineAt(line);
      const range = m[2]
        ? doc.getWordRangeAtPosition(new vscode.Position(line, +m[2] - 1)) ||
          new vscode.Range(line, +m[2] - 1, line, +m[2])
        : new vscode.Range(line, text.firstNonWhitespaceCharacterIndex, line, text.text.length);
      diags.set(doc.uri, [new vscode.Diagnostic(range, m[3], vscode.DiagnosticSeverity.Error)]);
    });
    p.stdin.on('error', () => {});
    p.stdin.end(doc.getText());
  };

  const debounced = (doc) => {
    clearTimeout(timers.get(doc.uri.toString()));
    timers.set(doc.uri.toString(), setTimeout(() => check(doc), 300));
  };

  const stdlib = new Map(); // name -> [{ sig, doc }]
  const exe = vscode.workspace.getConfiguration('ns').get('path') || 'ns';
  execFile(exe, ['doc'], { timeout: 10000 }, (err, out) => {
    if (err) return;
    for (const line of out.split('\n')) {
      const [sig, doc] = line.split('\t');
      if (!sig) continue;
      const name = sig.slice(0, sig.indexOf(' '));
      if (!stdlib.has(name)) stdlib.set(name, []);
      stdlib.get(name).push({ sig, doc });
    }
  });
  const types = new Map(); // uri -> { version, rows: Promise<[line, col, name, type][]> }
  const typesOf = (doc) => {
    const key = doc.uri.toString(), hit = types.get(key);
    if (hit && hit.version === doc.version) return hit.rows;
    const rows = new Promise((done) => {
      const p = execFile(exe, ['types', doc.fileName], { timeout: 10000 }, (_err, out) =>
        done((out || '').split('\n').filter(Boolean).map((l) => l.split('\t')).map(([l, c, n, t]) => [+l, +c, n, t])));
      p.stdin.on('error', () => {});
      p.stdin.end(doc.getText());
    });
    types.set(key, { version: doc.version, rows });
    return rows;
  };
  const varHover = async (doc, pos) => {
    const range = doc.getWordRangeAtPosition(pos, /[A-Za-z][A-Za-z0-9_]*'?|_[0-9]*|[@$]/);
    if (!range) return;
    const word = doc.getText(range).replace(/'$/, ''), line = range.start.line + 1, col = range.start.character + 1;
    const rows = await typesOf(doc);
    // the use at this position, else the same name elsewhere on the line (an assignment target, a parameter)
    let ts = rows.filter(([l, c]) => l === line && c === col);
    if (!ts.length) ts = rows.filter(([l, , n]) => l === line && n === word);
    if (!ts.length) return;
    const md = new vscode.MarkdownString();
    md.appendCodeblock(`${word}: ${[...new Set(ts.map((r) => r[3]))].join(' | ')}`, 'ns');
    return new vscode.Hover(md, range);
  };
  const hover = {
    async provideHover(doc, pos) {
      const v = await varHover(doc, pos);
      if (v) return v;
      const range = doc.getWordRangeAtPosition(pos, /[A-Za-z][A-Za-z0-9_]*/);
      const ovs = range && stdlib.get(doc.getText(range));
      if (!ovs || !ovs.some((o) => o.doc)) return; // undocumented = internal (operator helpers)
      const md = new vscode.MarkdownString();
      md.appendCodeblock(ovs.map((o) => o.sig).join('\n'), 'ns');
      for (const d of new Set(ovs.map((o) => o.doc).filter(Boolean))) md.appendMarkdown(`- ${d}\n`);
      return new vscode.Hover(md, range);
    },
  };

  context.subscriptions.push(
    vscode.languages.registerHoverProvider('ns', hover),
    diags,
    vscode.workspace.onDidOpenTextDocument(check),
    vscode.workspace.onDidChangeTextDocument((e) => debounced(e.document)),
    vscode.workspace.onDidCloseTextDocument((doc) => diags.delete(doc.uri)),
  );
  vscode.workspace.textDocuments.forEach(check);
}

module.exports = { activate };
