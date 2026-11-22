// Check for Harmony3StrictCompat WppFightTargetCheapValidatorMethod.MethodOf: the table keyed by
// (code address, second address, target type, delegate type) must return what Delegate.Method returns.
using System;
using System.Collections.Generic;
using System.Reflection;

class Box<T> { public bool Is(object o) => o is T; public static bool SIs(object o) => o is T; public bool G<U>(object o) => o is U; }
class Base { public virtual bool V(object o) => true; public bool N(object o) => false; }
class Derived : Base { public override bool V(object o) => false; }
class Derived2 : Base { }
struct S { public int x; public bool M(object o) => x > 0; }
static class P
{
    static readonly FieldInfo ptrF = typeof(Delegate).GetField("_methodPtr", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo auxF = typeof(Delegate).GetField("_methodPtrAux", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo cntF = typeof(MulticastDelegate).GetField("_invocationCount", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly Dictionary<(IntPtr, IntPtr, Type, Type), MethodInfo> known = new();
    static int real, hits;
    static MethodInfo MethodOf(Delegate d)
    {
        if (d is not MulticastDelegate m || (IntPtr)cntF.GetValue(m) != IntPtr.Zero) return d.Method;
        var key = ((IntPtr)ptrF.GetValue(d), (IntPtr)auxF.GetValue(d), d.Target?.GetType(), d.GetType());
        if (!known.TryGetValue(key, out var method))
        {
            real++; method = d.Method;
            if (method.DeclaringType is { } type && !type.Assembly.IsCollectible) known[key] = method;
        }
        else hits++;
        return method;
    }
    static bool St(object o) => true;
    static bool St2(object o) => false;
    static IEnumerable<Func<Delegate>> Shapes()
    {
        int cap = 3; string cap2 = "x";
        yield return () => new Predicate<object>(St);
        yield return () => new Predicate<object>(St2);
        yield return () => new Predicate<object>(o => cap > 0);          // closure class
        yield return () => new Predicate<object>(o => cap2 != null);     // second lambda, same closure class
        yield return () => new Predicate<object>(o => o != null);        // static lambda (cached, but take a fresh copy)
        yield return () => new Predicate<object>(new Box<string>().Is);  // shared canonical code,
        yield return () => new Predicate<object>(new Box<object>().Is);  // different instantiations
        yield return () => new Predicate<object>(new Box<int>().Is);
        yield return () => new Predicate<object>(Box<string>.SIs);
        yield return () => new Predicate<object>(Box<object>.SIs);
        yield return () => new Predicate<object>(new Box<int>().G<string>);
        yield return () => new Predicate<object>(new Box<int>().G<object>);
        yield return () => new Predicate<object>(new Base().V);
        yield return () => new Predicate<object>(new Derived().V);
        yield return () => new Predicate<object>(new Derived2().V);
        yield return () => new Predicate<object>(new Derived().N);
        yield return () => new Predicate<object>(new Derived2().N);
        yield return () => new Predicate<object>(new S { x = 1 }.M);
        yield return () => new Func<object, bool>(St);                    // other delegate type, same method
        yield return () => Delegate.CreateDelegate(typeof(Func<Base, object, bool>), typeof(Base).GetMethod("N"));       // open instance
        yield return () => Delegate.CreateDelegate(typeof(Func<Base, object, bool>), typeof(Base).GetMethod("V"));       // open virtual
        yield return () => Delegate.CreateDelegate(typeof(Func<Box<string>, object, bool>), typeof(Box<string>).GetMethod("Is"));
        yield return () => Delegate.CreateDelegate(typeof(Func<Box<object>, object, bool>), typeof(Box<object>).GetMethod("Is"));
        yield return () => Delegate.CreateDelegate(typeof(Predicate<object>), "abc", typeof(object).GetMethod("Equals", new[] { typeof(object) })); // closed over virtual
        yield return () => (Predicate<object>)Delegate.Combine(new Predicate<object>(St), new Predicate<object>(St2)); // multicast
        yield return () => (Predicate<object>)Delegate.Combine(new Predicate<object>(St2), new Predicate<object>(St));
    }
    static int Main()
    {
        int bad = 0, n = 0;
        for (int round = 0; round < 4; round++)
            foreach (var make in Shapes())
            {
                // Fresh delegates every time, like a turret's validator; the real answer comes from another fresh copy.
                var ours = MethodOf(make()); var theirs = make().Method; n++;
                if (!ReferenceEquals(ours, theirs) && ours != theirs) { bad++; Console.WriteLine($"MISMATCH round {round}: ours {ours.DeclaringType}.{ours} / real {theirs.DeclaringType}.{theirs}"); }
            }
        Console.WriteLine($"runtime {Environment.Version}: {n} checks, {bad} mismatches, {real} real lookups, {hits} table hits, {known.Count} keys");
        return bad == 0 && hits > 0 ? 0 : 1;
    }
}
