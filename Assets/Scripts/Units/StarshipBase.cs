using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;

using Fusion;

public class StarshipBase : NetworkBehaviour
{
    public Vector3 NetworkedTargetPosition { get; set; }
    //public Vector3 position { get; set; }
    //public Quaternion rotation { get; set; }
    //private Vector3 velocity = Vector3.zero;
    //private Vector3 omega = Vector3.zero;
    public int OwnerID { get; set; }
    //[Networked] public UnitState State { get; set; } // Custom enum

    public string shipClass = "Fighter";
    public string faction = "Neutral";
    public string shipName = "Unnamed Ship";
    public float shields  { get; set; }
    public float maxShields  { get; set; }
    public float hullIntegrity  { get; set; }
    public float maxHullIntegrity  { get; set; }
    public float engineHitPoints  { get; set; }
    public float maxEngineHitPoints  { get; set; }
    public float weaponHitPoints  { get; set; }
    public float maxWeaponHitPoints  { get; set; }
    public float shieldsHitPoints  { get; set; }
    public float maxShieldsHitPoints  { get; set; }
    public float lifeSupportHitPoints  { get; set; }
    public float maxLifeSupportHitPoints  { get; set; }
    public float powerHitPoints  { get; set; }
    public float maxPowerHitPoints  { get; set; }
    public float sensorHitPoints  { get; set; }
    public float maxSensorHitPoints  { get; set; }
    public float commsSystemHitPoints  { get; set; }
    public float maxCommsSystemHitPoints  { get; set; }
    public float energy  { get; set; }
    public float maxEnergy  { get; set; }
    public float morale  { get; set; }
    public float maxMorale  { get; set; }
    public int crew { get; set; }
    public int maxCrew { get; set; }
    public int cargo { get; set; }
    public int maxCargo { get; set; }

    public float probabiltyOfHitShieldSystem = 0.1f;
    public float probabiltyOfHitEngineSystem = 0.1f;
    public float probabiltyOfHitWeaponSystem = 0.1f;
    public float probabiltyOfHitLifeSupportSystem = 0.1f;
    public float probabiltyOfHitPowerSystem = 0.1f;
    public float probabiltyOfHitSensorSystem = 0.1f;
    public float probabiltyOfHitCommsSystem = 0.1f;
    public float probabiltyOfHitCrew = 0.1f;

    public float valueModifier = 1f;

    public float nominalSpeed = 5f;
    public float maxSpeed = 10f;
    public float nominalAcceleration = 2f;
    public float maxAcceleration = 5f;
    public float nominalTurnRate = 5f;
    public float maxTurnRate = 10f;
    public float shieldPowerConsumption = 10f;
    public float enginePowerConsumption = 10f;
    public float weaponPowerConsumption = 10f;
    public float lifeSupportPowerConsumption = 10f; 
    public float sensorPowerConsumption = 10f;
    public float commsSystemPowerConsumption = 10f;
    public float dilithiumPowerEfficiency = 0.01f; // Dilithium per power unit

    public float sensorRange = 100f;
    //private Quaternion targetRotation;
    public string modelPath;
    
    // Placeholder for system status enum
    public enum SystemStatus { Online, Damaged, Offline }

    // private void Update()
    // {
    //     if (Object.HasStateAuthority)
    //     {
    //         // Server updates movement
    //         //transform.position = position;
    //     }
    //     else
    //     {
    //         // Clients interpolate movement
    //         transform.position = Vector3.Lerp(transform.position, position, Time.deltaTime * 10);
    //     }
    // }

    // public void MoveTo(Vector3 targetPos, Quaternion targetRot)
    // {
    //     if (Object.HasStateAuthority)
    //     {
    //         position = targetPos; // Only the host updates the position
    //     }
    // }

    public void TakeDamage(float dmg)
    {
        if (Object.HasStateAuthority)
        {
            shields -= dmg;
            if (shields <= 0)
            {
                Runner.Despawn(Object); // Destroy on the network
            }
        }
    }

    public virtual void LoadFromConfig(string configPath)
    {
        DynamicLoader.PopulateObjectFromFile(this,configPath);
        LoadModel();
    }

    private void LoadModel()
    {
        SODLoader SL = new SODLoader();
        SL.LoadSOD(modelPath, gameObject);
    }

    public virtual void Move(Vector3 destination)
    {
        Vector3 direction = (destination - transform.position).normalized;
        //targetRotation = Quaternion.LookRotation(direction);
        //transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        
        if (Vector3.Angle(transform.forward, direction) < 10f)
        {
            //velocity = Vector3.Lerp(velocity, transform.forward * speed, acceleration * Time.deltaTime);
            //transform.position += velocity * Time.deltaTime;
        }
    }

    public virtual void Attack(StarshipBase target)
    {
        if (Vector3.Distance(transform.position, target.transform.position) <= sensorRange)
        {
            target.TakeDamage(10);
        }
    }
}

// Ship Factory
public class ShipFactory
{
    public static StarshipBase CreateShip(string configPath, Vector3 position, Quaternion rotation)
    {
        GameObject shipObject = new GameObject();
        shipObject.transform.position = position;
        shipObject.transform.rotation = rotation;
        StarshipBase ship = shipObject.AddComponent<StarshipBase>();
        ship.LoadFromConfig(configPath);
        return ship;
    
    }
}


