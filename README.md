# yaml-oracle

Differential testing of [YamlDotNet](https://github.com/aaubry/YamlDotNet)'s parser against
[libfyaml](https://github.com/pantoniou/libfyaml), a YAML 1.2 parser in C that passes the whole
[yaml-test-suite](https://github.com/yaml/yaml-test-suite). When the two disagree, the
[YAML 1.2 reference parser](https://github.com/yaml/yaml-reference-parser), which is generated from the spec's
grammar, decides which one is right.

Every parser's output is written in the yaml-test-suite's event format (`test.event`). Two parsers agree when they
produce the same events, or when both reject the input. A disagreement becomes a case directory in the suite's
layout, which drops into any harness that runs the suite, YamlDotNet's included.

## How it works

| Parser | How it runs |
|---|---|
| YamlDotNet | in the test process (`src/YamlOracle/YamlDotNetEvents.cs`) |
| libfyaml | in `YamlOracle.Worker`, a process that calls libfyaml by P/Invoke and stays up between inputs, so a native crash ends only the worker (`src/YamlOracle.Worker`) |
| reference parser | `node tools/reference/reference.js serve`, through the same protocol; it is slow, so it is only asked when the other two disagree |

The tests (`tests/YamlOracle.Tests`):

- **`TestSuiteTests`** runs the yaml-test-suite (data-2022-01-17, a submodule) through both parsers. libfyaml must
  pass all 402 cases. That's what makes it an oracle, and it also checks the P/Invoke declarations. YamlDotNet must
  pass all but its known failures.
- **`CaseTests`** runs our own cases (`tests/YamlOracle.Tests/cases`). Their expected events come from the reference
  parser.
- **`DifferentialProperties`** generates YAML with [CsCheck](https://github.com/AnthonyLloyd/CsCheck) and compares
  the two parsers. The inputs are suite and case inputs with small edits, plus documents built from YAML's
  constructs.
  - A difference that a known cause explains (`KnownDifferences.Causes`) is counted.
  - Otherwise the reference parser decides it:
    - **libfyaml is wrong:** the input is kept under `TestResults/libfyaml-bugs` and the search goes on;
    - **YamlDotNet is wrong:** the test fails, after making the input as small as it can, and keeps it under
      `TestResults/differences`.

`KnownDifferences` lists every known YamlDotNet failure. A listed failure that starts passing fails its test, so
the list stays true.

## What it has found in YamlDotNet 18.1.0

- **YamlDotNet's own spec tests skip 69 of the suite's 402 cases**, every case in a subdirectory (`4MUZ/02`, ...):
  `YamlDotNet.Test/Spec/SpecTestData.cs` enumerates only top-level directories. 17 of those 69 fail.
- **Any exception counts as an expected error in its spec tests**, so 9C9N passes there even though YamlDotNet
  throws `InvalidOperationException` instead of a YAML error.
- **Suite failures** (`KnownDifferences.SuiteFailures`):
  - rejects valid YAML: 4MUZ/02, DK95/04, JEF9/01, UKK6/00, VJP3/01;
  - reads valid YAML wrongly: JEF9/02, L24T/01, M2N8/00;
  - accepts invalid YAML: DK95/01, MUS6/00, Y79Y/003–009;
  - throws `InvalidOperationException`: 9C9N, VJP3/00.
- **Found by the oracle** (`tests/YamlOracle.Tests/cases`):
  - `star-word-in-plain-scalar`: a word starting with `*` in a plain scalar is taken for an alias, so the `: `
    after it doesn't end the scalar.
  - `empty-key`: an entry with an empty key (`: x`) anywhere but the start of a top-level line. YamlDotNet rejects
    it, drops its value, or reads extra entries.
  - `unicode-line-breaks`: NEL, LS and PS are treated as line breaks, as in YAML 1.1. YAML 1.2 made them ordinary
    characters (section 5.4).
  - `document-end-marker`: a stream starting with `...` throws `InvalidOperationException`, and a document that is
    only a tag or an anchor after `...` is rejected.
  - `block-scalar-spaces-only-lines`: with keep chomping (`|+`), the last spaces-only line of a stream that ends
    without a line break is dropped. Leading spaces-only lines longer than the content's indentation are rejected
    (the suite's JEF9 and L24T).
  - `reserved-indicator-starts-plain-scalar`: in a flow collection, a plain scalar may start with `@` or `` ` ``
    (`[@]`), which YAML reserves. YamlDotNet rejects this in block context.
  - `plus-in-tag`: a `+` in a tag is decoded as a space in a local tag (`!a+b` reads as `!a b`). In a global tag
    (`!!a+b`, verbatim, or a `%TAG` handle) it throws `ArgumentException`, not a YAML error.
  - `comment-without-blank`: a `#` right after `[` or `{` is taken for a comment, which needs a blank before it.
  - `empty-key/13`, `empty-key/14`: an explicit empty key in a flow sequence (`[? ]`) is rejected. `[?]` (invalid:
    an explicit key's `?` needs a blank after it) is accepted, by libfyaml too. Since the two parsers agree there, only
    the case finds it.
  - Tabs used as indentation are accepted (the suite's Y79Y and DK95).

libfyaml 1.0.0-rc2 isn't perfect either. `mapping-on-its-keys-line` holds one of its bugs: it accepts a block
mapping that starts on the line of its key's `:`. Its other differences from the reference parser end up in
`TestResults/libfyaml-bugs`.

Nor is the reference parser. It rejects every byte order mark, though YAML allows one at the start of a document
(section 5.2). So inputs with U+FEFF are a known cause rather than settled. Where the three parsers disagree with
each other and the grammar doesn't make the answer plain, there is no case.

## Running it

You need the .NET 10 SDK, CMake 3.20+ with a C compiler (MSVC, gcc or clang), and, to settle differences, Node.js 20+.

```sh
git clone --recurse-submodules https://github.com/fluentfoundation/yaml-oracle.git
cd yaml-oracle
cmake -P eng/libfyaml.cmake          # downloads libfyaml 1.0.0-rc2, checks its SHA-256, builds artifacts/native
npm ci --prefix tools/reference      # the reference parser's dependencies
dotnet test
```

- **Search time:** the property searches for 10 seconds by default. Set `CsCheck_Time` to search longer
  (`$env:CsCheck_Time = 300` in PowerShell, `CsCheck_Time=300 dotnet test` in sh). `CsCheck_Seed` replays a run.
- **Without the reference parser:** with no Node or no `npm ci`, every difference fails the property; nothing
  decides between the two parsers.
- **Evaluating a YamlDotNet fix:** run against a source tree instead of the package:
  `dotnet test -p:YamlDotNetSource=../YamlDotNet/YamlDotNet/YamlDotNet.csproj`. Known failures the fix repairs then
  fail as "passes now", which is the point.
- **Confirming kept cases:** `node tools/reference/reference.js check TestResults/differences` re-checks kept cases
  against the reference parser. `check tests/YamlOracle.Tests/cases` checks our expected results (CI runs it).
- **Line endings:** the suite's inputs are LF in its repository. A checkout with `core.autocrlf=true` turns them
  into CRLF; the tests read them back as LF. Our own cases are marked `-text` in `.gitattributes`, so their bytes are
  exact.

## Reporting a difference

1. Confirm it: `node tools/reference/reference.js check TestResults/differences/<dir>`.
2. Add it to `tests/YamlOracle.Tests/cases/<topic>/<nn-name>/`:
   - `in.yaml`;
   - `===`, a one-line title;
   - `test.event` from `node tools/reference/reference.js events in.yaml`, or an empty `error` file if the reference
     parser rejects it.
3. List it in `KnownDifferences`. If generated inputs keep hitting it, add a `Cause`: a rewrite that removes what
   triggers it, so the search can go past it.
4. The directory is what goes upstream: it's a yaml-test-suite case, so it drops into YamlDotNet's spec tests.

## Licence

MIT (`LICENSE`).
- **libfyaml** (MIT) is downloaded and built at build time, not stored here.
- **The yaml-test-suite** (MIT) and **the reference parser** are git submodules. The reference parser's repository
  states no licence, so it is only run from the submodule, not copied.
