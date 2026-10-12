# AE version metadata research

The packed AE version reader was independently implemented in C# using the bit-field research in:
https://github.com/tinogithub/aftereffects-version-check

Additional RIFX/header structure references:
https://github.com/boltframe/aftereffects-aep-parser
https://github.com/itsAnchorpoint/AEP-Downgrader/blob/main/docs/technical_documentation.md

Only read-only inspection is implemented here. This program does not patch, downgrade, or rewrite AEP headers. These are community-researched formats, not an Adobe-guaranteed compatibility API. The format marker is cross-checked against the packed saving-version fields; unknown or contradictory records fail explicitly.

## MIT License — tinogithub/aftereffects-version-check

Copyright (c) 2023 tinogithub

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

Microsoft WebView2 redistributable SDK licenses and notices are supplied in lib.
