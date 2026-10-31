/// Borrowed parameters.
///
/// Lean 4's borrow inference, simplified. Every NS call passes owned (+1) arguments that the callee releases on
/// return: a retain/release pair per pointer argument per call, which LLVM can't remove (a release may free).
/// A parameter the function never reassigns or mutates, in a function that writes no captures/globals and no
/// reference value, is marked borrowed: callers lend it (an uncaptured local without a retain), the callee skips
/// its release, and the C callback wrapper skips its retain. It also takes the release off the path after a
/// self tail call, so LLVM's tail-recursion elimination can turn it into a loop.
module Features.Opt.Borrow

open Opt

let private inferBorrows (cx: Ctx) (insts: Typer.Inst list) =
    for i in insts do
        let s = newSum ()
        List.iter (sumS cx s) i.body
        sumE cx s i.result
        sumE cx s i.result
        let ext = cx.info.[i.name]
        i.borrowed.Clear()
        if ext.writes.Count = 0 && not ext.refWrite then
            for (p, _) in i.parms do
                if not (s.writes.Contains p) then i.borrowed.Add p |> ignore

let pass : Opt.Pass =
    { name = "borrows"
      order = 70
      run = fun cx prog -> inferBorrows cx prog.insts; prog }
