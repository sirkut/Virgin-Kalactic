using System;
using UnityEngine;
using KSP;
using System.Linq;
using System.Collections.Generic;

namespace Virgin_Kalactic
{
	
	public class BetterPart : Part
	{
		public override float RequestResource (int resourceID, float demand)
		{
			return (float)RequestResource(resourceID, (double)demand);
		}

		public override float RequestResource (string resourceName, float demand)
		{
			return (float)RequestResource(resourceName, (double)demand);
		}

		public override double RequestResource (int resourceID, double demand)
		{
			return RequestResource(PartResourceLibrary.Instance.GetDefinition(resourceID).name, demand);
		}
		
		public override double RequestResource (string resourceName, double demand)
		{
			TrackResource tr = DictionaryManager.GetTrackResourceForVessel (vessel);
			double accepted = base.RequestResource (resourceName, demand);
			tr.Sample (resourceName, demand, accepted);
			return accepted;
		}
	}
	
	[KSPAddon (KSPAddon.Startup.Flight, false) ]
	public class DictionaryManager : UnityEngine.MonoBehaviour
	{
		private static Dictionary<Vessel, TrackResource> vesselResourceDict = new Dictionary<Vessel, TrackResource>();
        static DictionaryManager Instance;          
		
		public void Start()
		{
			GameEvents.onVesselGoOffRails.Add (CTRFV);
			GameEvents.onVesselGoOnRails.Add (RVFD);
			GameEvents.onVesselWillDestroy.Add (RVFD);
            Instance = this;
		}
		
		public void onDestroy()
		{
			GameEvents.onVesselGoOffRails.Remove (CTRFV);
			GameEvents.onVesselGoOnRails.Remove (RVFD);
			GameEvents.onVesselWillDestroy.Remove (RVFD);
		}
		
		private void CTRFV (Vessel v)
		{
			CreateTrackResForVessel (v);
		}
		
		private static void CreateTrackResForVessel (Vessel v)
		{
			if (!vesselResourceDict.ContainsKey (v))
			{
				TrackResource tr = DictionaryManager.Instance.gameObject.AddComponent<TrackResource> ();     
				vesselResourceDict.Add(v, tr);
			} else {
				Debug.Log ("CreateTrackResForVessel: TrackRes Already Exists for Vessel" + v.GetName ());
				//Debug.Log (System.Environment.StackTrace);
			}
			
		}
		
		private void RVFD (Vessel v)
		{
			RemoveVesselFromDict (v);
		}
		
		private static void RemoveVesselFromDict (Vessel v)
		{
			vesselResourceDict.Remove(v);
		}
		
		public static TrackResource GetTrackResourceForVessel (Vessel v)
		{
			TrackResource tr;
			if (!vesselResourceDict.TryGetValue(v, out tr))
			{
				CreateTrackResForVessel(v);
				tr = vesselResourceDict[v];
			}
			return tr;
		}
	}
	
	public class TrackResource : MonoBehaviour
	{
		
		private Dictionary<string, double> consumption = new Dictionary<string, double>();
		private Dictionary<string, double> generation = new Dictionary<string, double>();
		private Dictionary<string, double> sumConsumption = new Dictionary<string, double>();
		private Dictionary<string, double> sumGeneration = new Dictionary<string, double>();
		
		public void Sample (string resourceName, double demand, double accepted)
		{
			if (demand == 0) { return; }
			if (demand > 0)
			{
				if (!sumConsumption.ContainsKey(resourceName))
				{
					sumConsumption.Add (resourceName, 0);
				}
				sumConsumption[resourceName] += demand;
				
			} else {
				if (!sumGeneration.ContainsKey(resourceName))
				{
					sumGeneration.Add (resourceName, 0);
				}
				sumGeneration[resourceName] += demand * -1;
				
			}
		}
		
		public double GetConsumption(string resourceName)
		{
			if (consumption.ContainsKey(resourceName))
			{
				return consumption[resourceName];
			} else {
				return 0;
			}
		}
		
		public double GetGeneration(string resourceName)
		{
			if (generation.ContainsKey(resourceName))
			{
				return generation[resourceName];
			} else {
				return 0;
			}
		}
		
		public void LateUpdate()
		{
			consumption = sumConsumption;
			generation = sumGeneration;
			
			sumConsumption = new Dictionary<string, double>();
			sumGeneration  = new Dictionary<string, double>();

		}
		
	}
	
	public class AdvGenerator: PartModule
	{
		//[KSPField(guiActive = true, guiName = "Responding to")]

		public List<Resource> inputs = new List<Resource>();
		public List<Resource> outputs = new List<Resource>();
		
		[KSPField]
		public int numSamples = 20;
		private int curSample = 0;
		
		[KSPField(guiActive = true, guiName = "Demand")]
		public double demand;
		
		[KSPField(isPersistant = true)]
		public bool activen = false; //Todo: name this something less stupid
		
		private TrackResource tr;
		
		public class Resource
    	{
        	private PartResourceDefinition _resource = new PartResourceDefinition();
    		public PartResourceDefinition resource
    		{
        		get { return this._resource; }
    		}

    		private double _maxRate = 1;
    		public double maxRate
    		{
				get { return this._maxRate; }
    		}

    		private FloatCurve _rateCurve = new FloatCurve();
    		public FloatCurve rateCurve
    		{
        		get { return this._rateCurve; }
    		}
			
			public double[] samples;

    		public Resource(ConfigNode node)
    		{
        		if (node.HasValue("resourceName") && PartResourceLibrary.Instance.resourceDefinitions.Any(d => d.name == node.GetValue("resourceName")))
        		{
        			this._resource = PartResourceLibrary.Instance.GetDefinition(node.GetValue("resourceName"));
        			if (node.HasValue("maxRate")) { double.TryParse(node.GetValue("maxRate"), out _maxRate); }
            		if (node.HasNode("rateCurve")) { _rateCurve.Load(node.GetNode("rateCurve")); }
            	}
    		}
    	}
		
		public override void OnStart(PartModule.StartState state)
		{
			foreach (Resource item in outputs)
			{
				item.samples = new double[numSamples];
			}
		}
		public override void OnUpdate()
		{
			if (!tr) { tr = DictionaryManager.GetTrackResourceForVessel (vessel); }
			if (activen) 
			{
				foreach (Resource item in outputs)
				{
					item.samples[curSample] = tr.GetConsumption(item.resource.name);
					curSample = (curSample + 1) % numSamples;
					//demand = samples.Average();
				}
			}
		}
		
		[KSPEvent(guiActive = true, guiName = "Activate")]
		public void Activate()
		{
			activen = true;
			Events["Activate"].active = false;
			Events["Deactivate"].active = true;
		}
		
		[KSPEvent(guiActive = true, guiName = "Deactivate", active = false)]
		public void Deactivate()
		{
			activen = false;
			Events["Activate"].active = true;
			Events["Deactivate"].active = false;
		}
	}
}
	 
//	public class BalloonAnimator : PartModule
//	{
//		
//		public override void OnStart(PartModule.StartState state)
//		{
//			//Set Part Animation State to match current contents
//		}
//		public override void OnUpdate()
//		{
			//this.part.Resources
			//Update Part Animation State to match current contents
//		}
//	}


//	public class ModuleDustClouds : PartModule
//	{
//               
//		WheelCollider Wub;
//               
//		[KSPField]
//		public float minSpeed;
//               
//		public override void OnStart(PartModule.StartState state)
//		{
//			if (HighLogic.LoadedSceneIsFlight)
//			{
//				Wub = this.part.Modules.OfType<ModuleWheel>().SelectMany(m => m.wheels).Select(w => w.whCollider).First();
//			}
//		}
//               
//		public override void OnUpdate()
//		{
//                       
//			float Torque = Math.Abs(Wub.motorTorque);
//			float Speed = this.vessel.GetSrfVelocity().magnitude;
//                       
//			if (Wub.isGrounded && Speed > this.minSpeed)
//			{
//				Debug.Log("speed " + Speed + " ||| Torque " + Torque);
				// spawn dust
//			}
//		}
//
//		
//	}
//}
