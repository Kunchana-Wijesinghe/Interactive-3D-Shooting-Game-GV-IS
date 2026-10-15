using System;
using FacilityBreach.Core;
class Program {
 static int n;
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);n++;}
 static void Main(){
 for(int offset=0;offset<100;offset++) {
  var m=new MissionRun();m.Start();m.Tick(290+offset*.1);m.CollectCard();m.Connect(true);m.Tick(20);
  Check(m.State==(offset<50?MissionState.Escape:MissionState.Lost),"timeout boundary "+offset);
 }
 var r=new PlayerResources();int total=275;int spent=0;
 var random=new Random(7);
 for(int i=0;i<10000;i++) {
  switch(random.Next(3)){case 0:r.Swap(random.Next(3));break;case 1:if(r.Shoot())spent++;break;case 2:r.Reload();break;}
  int equipped=r.Equipped,actual=r.Reserve;
  for(int w=0;w<3;w++){r.Swap(w);Check(r.Magazine>=0&&r.Magazine<=r.Capacities[w],"capacity");actual+=r.Magazine;}
  r.Swap(equipped);Check(actual+spent==total,"ammo conservation");Check(r.Reserve>=0,"reserve");
 }
 var pause=new MissionRun();pause.Start();pause.Paused=true;pause.Tick(1000);Check(pause.Remaining==300,"pause");
 pause.Paused=false;pause.Tick(double.NaN);pause.Tick(double.PositiveInfinity);Check(pause.Remaining==300,"finite time");
 Console.WriteLine("PASS "+n+" assertions: boundary sweep and seeded ammo conservation.");
 }
}