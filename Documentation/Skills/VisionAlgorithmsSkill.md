# Vision Algorithms Skill

## Core Pattern

Implement algorithmic Vision code as allocation-free reusable processors.

Prefer this shape:

1. Public array-based entry point.
2. Pin all input/output buffers with `fixed`.
3. Delegate immediately to a private pointer-based core method.
4. In hot loops, iterate with pointers and end pointers.
5. Keep pointer APIs private or internal to `FoosVision.Vision`.
6. Reuse preallocated buffers owned by the processor, finder, session, or caller.
7. Do not allocate inside per-frame algorithm paths.

Use the existing style from:

- `Product/Infrastructure/FoosVision.Vision/TableScene/Processing/`
- `Product/Infrastructure/FoosVision.Vision/BallFinding/Processing/`

## Buffer Ownership

Allocate image buffers, scratch buffers, segment buffers, result buffers, lookup tables,
and intermediate arrays once in constructors, setup code, or an explicit caller-owned workspace.

Do not allocate in methods that run per frame, per region, per row, or per pixel. This includes:

- `new byte[]`, `new int[]`, `new List<T>()`, and other dynamic collections
- LINQ materialization
- iterator allocations in hot paths
- temporary arrays for intermediate results
- closures or delegates inside processing loops
- debug buffers allocated per frame

If output size is bounded, pass an output buffer and return a count or encoded length.
If reusable state belongs to an algorithm, store it as fields on the algorithm class and
clear or overwrite only the required portions.

## Hot Loop Style

Use public wrappers to keep callers safe and ordinary:

```csharp
public static unsafe class ExampleAlgorithm
{
    public static int Process(byte[] input, byte[] output, int pixelCount)
    {
        fixed (byte* pInput = input)
        fixed (byte* pOutput = output)
            return Process(pixelCount, pInput, pOutput);
    }

    private static int Process(int pixelCount, byte* pInput, byte* pOutput)
    {
        byte* pSrc = pInput;
        byte* pSrcEnd = pInput + pixelCount;
        byte* pDst = pOutput;

        while (pSrc < pSrcEnd)
        {
            // Hot path.
            pSrc++;
            pDst++;
        }

        return (int)(pDst - pOutput);
    }
}
```

Prefer pointer end checks such as `pSrc < pSrcEnd` and `pOut < pOutEnd` over repeated array indexing in pixel-level loops.
Use local variables for values accessed repeatedly in the loop. Add `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
to tiny helpers that are demonstrably on the hot path and match existing local style.

## Hot Path Math And Framework Calls

Avoid expensive framework calls inside per-pixel, per-sample, per-row, or per-column loops.
Review these explicitly before finishing algorithm code:

- Prefer squared-distance comparisons over `Math.Sqrt`; store precomputed squared thresholds such as `RadiusSquared`
  when a model is created.
- Avoid `Convert.ToInt32`, `Convert.ToByte`, `Math.Round`, `Math.Ceiling`, `Math.Floor`, and `Math.Clamp` in hot loops.
  If rounding, ceiling, or clamping is unavoidable, normalize values once before the loop or use a small local helper
  with clear input assumptions.
- Avoid repeated option validation such as `Math.Max(0, option)` inside hot loops. Normalize options in constructors
  or setup methods and store the normalized values in fields.
- Avoid allocating result arrays or records from high-frequency processing calls. Provide caller-owned output buffers
  and return counts or ranges. Keep object-shaped results only for diagnostics, tests, or rare calibration paths.
- Keep small pixel predicates and color conversion helpers simple enough for the JIT to inline.

## Boundaries

Keep unsafe and algorithm-specific details inside `FoosVision.Vision`.

Do not expose pointer-based APIs across project boundaries. Do not move Vision algorithm details into adapters, protocol,
use cases, or domain. Debug visualization transport may be exposed through ports, but raw image-processing internals
should remain in Vision.

## Avoid

- LINQ, `foreach`, enumerators, or `yield` in pixel-level or frame-level hot paths.
- Allocating per-frame intermediate buffers.
- Adding broad generic abstractions around hot loops.
- Recomputing dimensions, strides, or offsets inside inner loops when they can be prepared once.
- Changing an algorithm's ownership model just to make a local call easier.

## Verification

After algorithm changes, run at least the matching Vision unit tests.
Run a broader build or tests when public APIs, project boundaries, unsafe code, shared buffers, or reusable state changed.
