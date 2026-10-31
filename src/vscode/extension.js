// Live diagnostics: pipes the buffer to `ns check <file>` and turns `path:line[:col]: error: msg` into a squiggle.
// Hovers: stdlib signatures and docs from `ns doc` (one `signature<TAB>doc` line per overload), loaded once.
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
  const hover = {
    provideHover(doc, pos) {
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
