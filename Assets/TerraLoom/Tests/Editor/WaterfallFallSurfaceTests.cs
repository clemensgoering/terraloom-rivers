using System;
using TerraLoom.Core;
#if UNITY_EDITOR
using NUnit.Framework;
#endif
namespace TerraLoom.Rivers.Tests
{
    public static class WaterfallFallSurfaceFixture
    {
        private static void Require(bool value){if(!value)throw new Exception("Parametric waterfall failed.");}
        private static void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid fall accepted.");}
        public static void Check(int scenario)
        {
            var lip=new WorldPoint(0,3.2,0);var impact=new WorldPoint(0,0,0);
            var p=new WaterfallFallSurface(lip,impact,0,1,3);
            switch(scenario)
            {
                case 0:
                    Require(p.Version==2&&p.HorizontalRun==0&&p.ArcLength==3.2);
                    var a=p.Sample(.2);var b=p.Sample(.8);
                    Require(a.X==b.X&&a.Z==b.Z&&a.Y>b.Y);break;
                case 1:
                    for(int i=0;i<=100;i++){var q=p.Sample(i/100d,1.5);Require(q.X==1.5&&q.Z==0&&Math.Abs(q.Y-(3.2-3.2*i/100d))<1e-12);}
                    Require(p.DistanceAt(1)==p.ArcLength);break;
                case 2:
                    var near=new WaterfallFallSurface(lip,new WorldPoint(0,0,.025),0,1,3);
                    Require(Math.Abs(near.Sample(.5).Z-.0125)<1e-12&&near.ArcLength>3.2);break;
                case 3:
                    var yaw=new WaterfallFallSurface(new WorldPoint(10,8.2,20),new WorldPoint(10,5,20),1,0,3);
                    Require(yaw.Across.Z==-1&&yaw.Sample(.5,1.5).Z==18.5&&Math.Abs(yaw.Sample(.5).Y-6.6)<1e-12);break;
                case 4:
                    Reject(()=>new WaterfallFallSurface(lip,impact,0,0,3));Reject(()=>new WaterfallFallSurface(lip,new WorldPoint(1,0,0),0,1,3));
                    Reject(()=>new WaterfallFallSurface(lip,new WorldPoint(0,0,-1),0,1,3));Reject(()=>new WaterfallFallSurface(lip,lip,0,1,3));break;
                case 5:
                    Reject(()=>p.Sample(double.NaN));Reject(()=>p.Sample(1.01));Reject(()=>p.Sample(.5,1.51));Reject(()=>p.Sample(.5,double.PositiveInfinity));
                    Reject(()=>new WaterfallFallSurface(lip,impact,double.PositiveInfinity,1,3));break;
                default:throw new ArgumentOutOfRangeException();
            }
        }
    }
#if UNITY_EDITOR
    public sealed class WaterfallFallSurfaceTests
    {
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)][TestCase(4)][TestCase(5)]
        public void ParametricFall(int scenario)=>WaterfallFallSurfaceFixture.Check(scenario);
    }
#endif
}
