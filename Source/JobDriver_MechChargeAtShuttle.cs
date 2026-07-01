using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace OdysseyShuttleVariants
{
    // A colony mech recharging at a landed mechanitor shuttle (our chemfuel field charger), instead
    // of a vanilla Building_MechCharger. TargetA = the shuttle; TargetB = the charge cell beside it.
    // Modelled on JobDriver_MechCharge: walk to the spot, stand and pump energy until topped up or
    // the shuttle stops charging (toggled off / out of fuel). See CompMechChargerShuttle.
    public class JobDriver_MechChargeAtShuttle : JobDriver
    {
        private CompMechChargerShuttle Charger => job.targetA.Thing?.TryGetComp<CompMechChargerShuttle>();

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            // Reserve the charge cell so two mechs don't fight over the same spot; the hull itself is
            // shared (many mechs may charge at once), so it isn't reserved.
            return pawn.Reserve(job.targetB.Cell, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Charger == null || !Charger.CanChargeNow(pawn));

            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell)
                .FailOnForbidden(TargetIndex.A);

            // Charging visuals reused from the vanilla mech charger: the glow mote attached to the mech
            // and the charging sustainer. (The mech->hull cable is drawn by CompMechChargerShuttle.)
            Mote moteCharging = null;
            Sustainer sustainer = null;

            Toil charge = ToilMaker.MakeToil("MechChargeAtShuttle");
            charge.defaultCompleteMode = ToilCompleteMode.Never;
            charge.handlingFacing = true;
            charge.initAction = delegate
            {
                Charger.RegisterCharging(pawn);
                sustainer = SoundDefOf.MechChargerCharging.TrySpawnSustainer(SoundInfo.InMap(pawn));
            };
            charge.AddFinishAction(delegate
            {
                Charger?.DeregisterCharging(pawn);
                sustainer?.End();
            });
            charge.tickIntervalAction = (Action<int>)delegate
            {
                pawn.rotationTracker.FaceTarget(job.targetA.Thing.Position);
                if (!Charger.ChargeTick(pawn))
                {
                    // Tank ran dry mid-charge.
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                if (moteCharging == null || moteCharging.Destroyed)
                {
                    moteCharging = MoteMaker.MakeAttachedOverlay(pawn, ThingDefOf.Mote_MechCharging, Vector3.zero);
                }
                moteCharging?.Maintain();
                sustainer?.Maintain();
                if (pawn.needs.energy.CurLevel >= JobGiver_GetEnergy.GetMaxRechargeLimit(pawn))
                {
                    ReadyForNextToil();
                }
            };
            yield return charge;
        }
    }
}
