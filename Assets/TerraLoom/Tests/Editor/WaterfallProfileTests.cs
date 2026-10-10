using System;
using TerraLoom.Core;
#if UNITY_EDITOR
using NUnit.Framework;
#endif
namespace TerraLoom.Rivers.Tests
{
    // Same cases execute without Unity in Tools/WaterfallProfileChecks.csproj.
    public static class WaterfallProfileFixture
    {
        public static WaterfallProfile Profile()=>new WaterfallProfile(new WorldPoint(0,3.2,-1),new WorldPoint(0,0,2.3),
            new WorldPoint(0,0,4),new WorldPoint(0,0,8),3.2,0,3,4.2,.75,1.1,.5,.012,.035,3);
        private static void Require(bool ok){if(!ok)throw new Exception("Waterfall profile contract failed.");}
        private static void Near(double a,double b,double tolerance=1e-8)=>Require(Math.Abs(a-b)<tolerance);
        private static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid profile accepted.");}
        public static void Check(int index)
        {
            var p=Profile();
            switch(index)
            {
                case 0:
                    Near(p.Sample(0).Surface.Y,3.2);Near(p.Sample(p.ImpactStation).Surface.Y,0);
                    Near(p.Sample(p.PoolStation).HalfWidth,2.1);Near(p.Sample(p.OutletStation).Surface.Y,0);
                    Require(p.Version==WaterfallProfileVersion.AnchoredPoolV1);break;
                case 1:
                    double previous=p.Sample(-1).Surface.Y;
                    for(int i=0;i<=20000;i++){double y=p.Sample(-1+i*.001).Surface.Y;Require(y<=previous+1e-12);previous=y;}break;
                case 2:
                    for(int i=0;i<=1000;i++)Near(p.Sample(p.ImpactStation+(p.OutletStation-p.ImpactStation)*i/1000).Surface.Y,0);
                    Require(p.Sample(p.OutletStation+4).Surface.Y<0);break;
                case 3:
                    Near(p.Sample(p.PoolStation).CentreDepth,1.1);Near(p.Sample(p.OutletStation).CentreDepth,.5);
                    Require(p.Sample(p.PoolStation).Surface.Y-p.Sample(p.PoolStation).CentreDepth<
                        p.Sample(p.OutletStation).Surface.Y-p.Sample(p.OutletStation).CentreDepth);break;
                case 4:
                    const double h=.00001;
                    Near((p.Sample(h).Surface.Y-p.Sample(-h).Surface.Y)/(2*h),-.012,.00002);
                    Near((p.Sample(p.ImpactStation+h).Surface.Y-p.Sample(p.ImpactStation-h).Surface.Y)/(2*h),0,.00002);
                    Near((p.Sample(p.OutletStation+h).Surface.Y-p.Sample(p.OutletStation-h).Surface.Y)/(2*h),0,.00002);
                    Near((p.Sample(p.OutletStation+3+h).Surface.Y-p.Sample(p.OutletStation+3-h).Surface.Y)/(2*h),-.035,.00002);break;
                case 5:
                    var rotated=new WaterfallProfile(new WorldPoint(10,8.2,20),new WorldPoint(13.3,5,20),new WorldPoint(15,5,20),new WorldPoint(19,5,20),8.2,5,3,4.2,.75,1.1,.5,.012,.035,3);
                    for(int i=0;i<100;i++){var a=p.Sample(i*.1);var b=rotated.Sample(i*.1);Near(a.Surface.Y+5,b.Surface.Y);Near(a.HalfWidth,b.HalfWidth);Near(b.Surface.X,10+i*.1);}break;
                case 6:
                    Reject(()=>new WaterfallProfile(p.Lip,p.Impact,new WorldPoint(1,0,4),p.Outlet,3.2,0,3,4.2,.75,1.1,.5));
                    Reject(()=>new WaterfallProfile(p.Lip,p.Impact,p.Outlet,p.Pool,3.2,0,3,4.2,.75,1.1,.5));
                    Reject(()=>new WaterfallProfile(p.Lip,new WorldPoint(0,.1,2.3),p.Pool,p.Outlet,3.2,0,3,4.2,.75,1.1,.5));break;
                case 7:
                    foreach(double width in new[]{2.9,4.6,double.NaN,double.PositiveInfinity})Reject(()=>new WaterfallProfile(p.Lip,p.Impact,p.Pool,p.Outlet,3.2,0,3,width,.75,1.1,.5));
                    Reject(()=>new WaterfallProfile(p.Lip,p.Impact,p.Pool,p.Outlet,3.2,0,3,4.2,.75,1.1,.5,4));break;
                case 8:
                    Reject(()=>new WaterfallProfile(new WorldPoint(0,3.2,0),new WorldPoint(0,0,1000001),new WorldPoint(0,0,1000002),new WorldPoint(0,0,1000003),3.2,0,3,4.2,.75,1.1,.5,0));
                    var boundary=new WaterfallProfile(new WorldPoint(0,3.2,0),new WorldPoint(0,0,999998),new WorldPoint(0,0,999999),new WorldPoint(0,0,1000000),3.2,0,3,4.2,.75,1.1,.5,0);
                    foreach(var anchor in new[]{boundary.Lip,boundary.Impact,boundary.Pool,boundary.Outlet})Near(boundary.Sample(boundary.StationAt(anchor.X,anchor.Z)).Surface.Y,anchor.Y);
                    Reject(()=>p.Sample(double.NaN));Reject(()=>p.Sample(double.PositiveInfinity));Reject(()=>p.Sample(1000001));
                    Reject(()=>new WaterfallProfile(p.Lip,p.Impact,p.Pool,p.Outlet,3.2,0,3,4.2,.75,.7,.5));
                    Reject(()=>new WaterfallProfile(p.Lip,p.Impact,p.Pool,p.Outlet,3.2,0,3,4.2,.75,1.1,0));break;
                case 9:
                    var old=WaterfallProfile.LandscapeBaseline();Require(old.Version==WaterfallProfileVersion.LandscapeBaselineV0);
                    Near(old.SampleAtWorldPosition(0,98).Surface.Y,15.88,.00001);
                    Near(old.SampleAtWorldPosition(0,78).Surface.Y,8.12,.00001);
                    Near(old.SampleAtWorldPosition(0,78).CentreDepth,1.85,.00001);
                    Require(old.SampleAtWorldPosition(0,86).Section==WaterfallSection.Fall&&old.SampleAtWorldPosition(0,90).Section==WaterfallSection.Upstream);break;
                default:throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
#if UNITY_EDITOR
    public sealed class WaterfallProfileTests
    {
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)]
        [TestCase(5)][TestCase(6)][TestCase(7)][TestCase(8)][TestCase(9)]
        public void ExplicitProfileContract(int scenario)=>WaterfallProfileFixture.Check(scenario);
    }
#endif
}
