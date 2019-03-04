
# TODO

General bits:
* Resize
* Buffer managerment and scrollback support
* Rendering glitches in Mac port (the in-between line thing)
* Mouse support
* Caret shape support


Porting:

- [ ] AccessibilityManager.ts
- [x] Buffer.ts
- [x] BufferLine.ts
- [ ] BufferReflow.ts
- [x] BufferSet.ts
- [x] CharWidth.ts
- [ ] CompositionHelper.ts
- [x] EscapeSequenceParser.ts
- [x] InputHandler.ts
- [ ] Linkifier.ts
- [ ] SelectionManager.ts
- [ ] SelectionModel.ts
- [ ] SoundManager.ts
- [   ] Strings.ts
- [ ] Terminal.integration.ts
- [ ] Terminal.ts
- [ ] Types.ts
- [ ] Viewport.ts
- [ ] xterm.ts
- [x] common/CircularList.ts
- [ ] common/Clone.ts
- [ ] common/EventEmitter.ts
- [ ] common/Lifecycle.ts
- [ ] common/TypedArrayUtils.ts
- [ ] common/Types.ts
- [x] common/data/EscapeSequences.ts
- [x] core/data/Charsets.ts
- [x] core/input/Keyboard.ts
- [ ] core/input/TextDecoder.ts
- [ ] core/Platform.ts
- [ ] Renderer/*
- [ ] ui/*

xterm.js from 490e46ce0c396d0129db85467e8d097c3a74a3f9

Mhm on my vacation laptop I used 857ae4b702b17381f6b862909a3570a6c3ab30b4

Implement wraparound = false (CSI ? Pm l -> 7)

The test 0008 fails because when we get to the column 80, we have the cursor at 80, but somehow the behavior is that the backspace starts one column before
