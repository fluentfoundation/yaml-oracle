#!/usr/bin/env node
// The YAML 1.2 reference parser (external/yaml-reference-parser, generated from the spec's grammar) on
// yaml-test-suite style case directories: in.yaml, plus test.event (the events) or error (the input is invalid).
//
//   node reference.js events FILE      prints FILE's events, or "ERROR"
//   node reference.js serve            parses inputs from stdin, for YamlOracle (WorkerProtocol)
//   node reference.js check DIR...     checks every case under each DIR against the reference parser:
//                                        test.event / error  must match it (our own cases: exit code 1 if not)
//                                        libfyaml.event      a difference YamlOracle found: says whether the
//                                                            reference parser agrees with libfyaml
//
// It is slow (a backtracking parser), so it confirms what libfyaml finds rather than replacing it.
'use strict';
const fs = require('fs');
const path = require('path');

// the generated parser requires its dependencies by name, from its own directory: resolve them from here
process.env.NODE_PATH = path.join(__dirname, 'node_modules');
require('module').Module._initPaths();
const lib = path.join(__dirname, '..', '..', 'external', 'yaml-reference-parser', 'parser-1.2', 'javascript', 'lib');
['prelude', 'parser', 'test-receiver'].forEach(m => require(path.join(lib, m)));

function events(text) {
  const parser = new Parser(new TestReceiver());
  try {
    parser.parse(text);
    return parser.receiver.output();
  } catch {
    return 'ERROR';
  }
}

function* caseDirs(dir) {
  if (fs.existsSync(path.join(dir, 'in.yaml'))) yield dir;
  for (const e of fs.readdirSync(dir, { withFileTypes: true }))
    if (e.isDirectory()) yield* caseDirs(path.join(dir, e.name));
}

// serve: YamlOracle's worker protocol (src/YamlOracle/WorkerProtocol.cs) on stdin and stdout, little-endian:
// request int32 count + UTF-8 input (-1: exit); response byte (0 parsed, 1 rejected), then the events and an empty
// message, each an int32 count + UTF-8.
function serve() {
  let pending = Buffer.alloc(0);
  const frame = (s) => { const b = Buffer.from(s, 'utf8'); const n = Buffer.alloc(4); n.writeInt32LE(b.length); return [n, b]; };
  process.stdin.on('data', chunk => {
    pending = Buffer.concat([pending, chunk]);
    while (pending.length >= 4) {
      const count = pending.readInt32LE(0);
      if (count === -1) process.exit(0);
      if (pending.length < 4 + count) break;
      const got = events(pending.subarray(4, 4 + count).toString('utf8'));
      pending = pending.subarray(4 + count);
      const rejected = got === 'ERROR';
      process.stdout.write(Buffer.concat([Buffer.from([rejected ? 1 : 0]), ...frame(rejected ? '' : got), ...frame(rejected ? 'the reference parser rejects it' : '')]));
    }
  });
  process.stdin.on('end', () => process.exit(0));
}

const read = f => fs.readFileSync(f, 'utf8');
const [command, ...args] = process.argv.slice(2);
if (command === 'serve' && args.length === 0) {
  serve();
} else if (command === 'events' && args.length === 1) {
  const got = events(read(args[0]));
  process.stdout.write(got === 'ERROR' ? 'ERROR\n' : got);
} else if (command === 'check' && args.length > 0) {
  let failed = 0, checked = 0;
  for (const root of args) {
    for (const dir of caseDirs(root)) {
      checked++;
      const got = events(read(path.join(dir, 'in.yaml')));
      const rel = path.relative(process.cwd(), dir);
      for (const [file, label] of [['test.event', 'expected'], ['libfyaml.event', 'libfyaml']]) {
        const f = path.join(dir, file);
        if (!fs.existsSync(f)) continue;
        const same = got === read(f) || (got === 'ERROR' && read(f).startsWith('ERROR'));
        if (label === 'expected' && !same) failed++;
        console.log(`${same ? 'agrees' : 'DIFFERS'}  ${rel} (${label})${same ? '' : `\n--- reference parser:\n${got}`}`);
      }
      if (fs.existsSync(path.join(dir, 'error'))) {
        if (got !== 'ERROR') failed++;
        console.log(`${got === 'ERROR' ? 'agrees' : 'DIFFERS'}  ${rel} (expected an error)${got === 'ERROR' ? '' : `\n--- reference parser:\n${got}`}`);
      }
    }
  }
  console.log(`${checked} cases, ${failed} expected results the reference parser disagrees with`);
  process.exit(failed ? 1 : 0);
} else {
  console.error('usage: node reference.js events FILE | check DIR...');
  process.exit(2);
}
