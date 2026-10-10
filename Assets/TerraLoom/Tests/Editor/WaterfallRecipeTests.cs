using System;
using TerraLoom.Core;
#if UNITY_EDITOR
using NUnit.Framework;
#endif
namespace TerraLoom.Rivers.Tests
{
    public static class WaterfallRecipeFixture
    {
        public static WaterfallRecipe Recipe(double run=0)=>new WaterfallRecipe(new WorldPoint(0,3.2,0),new WorldPoint(0,0,run),
            new WorldPoint(0,0,run+1.7),new WorldPoint(0,0,run+5.7),0,1,3,4.2,.75,1.1,.5,.012,.035,3);
        private static void Require(bool ok){if(!ok)throw new Exception("Combined waterfall recipe failed.");}
        private static void Near(double a,double b,double tolerance=1e-9)=>Require(Math.Abs(a-b)<tolerance);
        private static void Point(WorldPoint a,WorldPoint b){Near(a.X,b.X);Near(a.Y,b.Y);Near(a.Z,b.Z);}
        private static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid recipe accepted.");}
        public static void Check(int index)
        {
            var r=Recipe();
            switch(index)
            {
                case 0:
                    foreach(double run in new[]{0,.025,3.3})
                    {
                        var q=Recipe(run);Near(q.Fall.HorizontalRun,run);
                        for(int i=0;i<=32;i++)
                        {
                            double a=(i-16)*1.5/16;
                            Point(q.SampleUpperSurface(0,a),q.Fall.Sample(0,a));
                            Point(q.Fall.Sample(1,a),q.SampleLowerSurface(0,a));
                        }
                    }
                    break;
                case 1:
                    Near(r.PoolStation,1.7);Near(r.OutletStation,5.7);
                    Point(r.SampleLowerReach(r.PoolStation).Surface,r.Pool);
                    Point(r.SampleLowerReach(r.OutletStation).Surface,r.OutletSill);
                    Near(r.SampleLowerReach(r.PoolStation).HalfWidth,2.1);
                    Near(r.SampleLowerReach(0).CentreDepth,1.1);
                    Near(r.SampleLowerReach(r.OutletStation).CentreDepth,.5);
                    for(int i=0;i<=1000;i++)Near(r.SampleLowerReach(r.OutletStation*i/1000).Surface.Y,0);
                    break;
                case 2:
                    var v1=WaterfallProfileFixture.Profile();
                    for(int i=0;i<=10000;i++)
                    {
                        double station=i*.002;var a=v1.Sample(v1.ImpactStation+station);var b=r.SampleLowerReach(station);
                        Near(a.Surface.Y,b.Surface.Y);Near(a.HalfWidth,b.HalfWidth);Near(a.CentreDepth,b.CentreDepth);Require(a.Section==b.Section);
                    }
                    break;
                case 3:
                    const double angle=.73;double x=Math.Sin(angle),z=Math.Cos(angle);
                    WorldPoint Transform(WorldPoint p)=>new WorldPoint(100+p.X*z+p.Z*x,40+p.Y,200-p.X*x+p.Z*z);
                    var turned=new WaterfallRecipe(Transform(r.Fall.Lip),Transform(r.Fall.Impact),Transform(r.Pool),Transform(r.OutletSill),x,z,3,4.2,.75,1.1,.5,.012,.035,3);
                    for(int i=0;i<=100;i++)
                    {
                        Point(Transform(r.SampleUpperSurface(i*.1,.7)),turned.SampleUpperSurface(i*.1,.7));
                        Point(Transform(r.SampleLowerSurface(i*.1,.7)),turned.SampleLowerSurface(i*.1,.7));
                        Point(Transform(r.Fall.Sample(i/100d,.7)),turned.Fall.Sample(i/100d,.7));
                    }
                    break;
                case 4:
                    const double h=1e-5;
                    Near((r.SampleLowerReach(r.OutletStation+h).Surface.Y-r.SampleLowerReach(r.OutletStation-h).Surface.Y)/(2*h),0,1e-6);
                    Near((r.SampleLowerReach(r.OutletStation+3+h).Surface.Y-r.SampleLowerReach(r.OutletStation+3-h).Surface.Y)/(2*h),-.035,1e-6);
                    Near(r.SampleLowerReach(r.OutletStation+4).CentreDepth,.75);
                    Near(r.SampleUpperReach(1).Surface.Y,3.212);
                    break;
                case 5:
                    foreach(var pool in new[]{new WorldPoint(1,0,1.7),new WorldPoint(0,.1,1.7),new WorldPoint(0,0,0),new WorldPoint(0,0,6)})
                        Reject(()=>new WaterfallRecipe(r.Fall.Lip,r.Fall.Impact,pool,r.OutletSill,0,1,3,4.2,.75,1.1,.5));
                    Reject(()=>new WaterfallRecipe(r.Fall.Lip,r.Fall.Impact,r.Pool,new WorldPoint(0,0,1000001),0,1,3,4.2,.75,1.1,.5));
                    Reject(()=>new WaterfallRecipe(r.Fall.Lip,r.Fall.Impact,r.Pool,r.OutletSill,0,0,3,4.2,.75,1.1,.5));
                    break;
                case 6:
                    foreach(double value in new[]{double.NaN,double.PositiveInfinity,-1,1000001})
                    {Reject(()=>r.SampleUpperReach(value));Reject(()=>r.SampleLowerReach(value));}
                    foreach(double lateral in new[]{double.NaN,double.PositiveInfinity,1.5001,-1.5001})
                    {Reject(()=>r.SampleUpperSurface(0,lateral));Reject(()=>r.SampleLowerSurface(0,lateral));}
                    Point(r.SampleLowerSurface(r.PoolStation,2.1),new WorldPoint(2.1,0,1.7));
                    break;
                case 7:
                    foreach(double width in new[]{2.9,4.6,double.NaN,double.PositiveInfinity})
                        Reject(()=>new WaterfallRecipe(r.Fall.Lip,r.Fall.Impact,r.Pool,r.OutletSill,0,1,3,width,.75,1.1,.5));
                    Reject(()=>new WaterfallRecipe(r.Fall.Lip,r.Fall.Impact,r.Pool,r.OutletSill,0,1,3,4.2,.75,.7,.5));
                    Reject(()=>new WaterfallRecipe(r.Fall.Lip,r.Fall.Impact,r.Pool,r.OutletSill,0,1,3,4.2,.75,1.1,0));
                    Reject(()=>new WaterfallRecipe(r.Fall.Lip,r.Fall.Impact,r.Pool,r.OutletSill,0,1,3,4.2,.75,1.1,.5,double.NaN));
                    break;
                default:throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
#if UNITY_EDITOR
    public sealed class WaterfallRecipeTests
    {
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)][TestCase(6)][TestCase(7)]
        public void CombinedPhysicalRecipe(int scenario)=>WaterfallRecipeFixture.Check(scenario);
    }
#endif
}
