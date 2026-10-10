using System;
using TerraLoom.Rivers.Tests;
internal static class WaterfallProfileChecks
{
    private static void Main(){for(int i=0;i<10;i++){WaterfallProfileFixture.Check(i);Console.WriteLine("PASS waterfall profile scenario "+i);}for(int i=0;i<6;i++){WaterfallFallSurfaceFixture.Check(i);Console.WriteLine("PASS parametric fall scenario "+i);}Console.WriteLine("Waterfall profiles: 16 passed, 0 failed; no Unity geometry evidence.");}
}
