using System;
using TerraLoom.Rivers.Tests;
internal static class WaterfallProfileChecks
{
    private static void Main(){for(int i=0;i<10;i++){WaterfallProfileFixture.Check(i);Console.WriteLine("PASS waterfall profile scenario "+i);}for(int i=0;i<6;i++){WaterfallFallSurfaceFixture.Check(i);Console.WriteLine("PASS parametric fall scenario "+i);}for(int i=0;i<8;i++){WaterfallRecipeFixture.Check(i);Console.WriteLine("PASS combined recipe scenario "+i);}Console.WriteLine("Waterfall profiles: 24 passed, 0 failed; no Unity geometry evidence.");}
}
