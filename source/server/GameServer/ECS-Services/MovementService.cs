using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using DOL.Logging;
using DOL.Timing;
using ECS.Debug;

namespace DOL.GS
{
    public sealed class MovementService : GameServiceBase
    {
        private static readonly Logger log = LoggerManager.Create(MethodBase.GetCurrentMethod().DeclaringType);

        private List<MovementComponent> _list;

        public static MovementService Instance { get; }

        static MovementService()
        {
            Instance = new();
        }

        public override void Tick()
        {
            ProcessPostedActionsParallel();
            int lastValidIndex;

            try
            {
                _list = ServiceObjectStore.UpdateAndGetAll<MovementComponent>(ServiceObjectType.MovementComponent, out lastValidIndex);
            }
            catch (Exception e)
            {
                if (log.IsErrorEnabled)
                    log.Error($"{nameof(ServiceObjectStore.UpdateAndGetAll)} failed. Skipping this tick.", e);

                return;
            }

            GameLoop.ExecuteForEach(_list, lastValidIndex + 1, TickInternal);

            if (Diagnostics.CheckServiceObjectCount)
                Diagnostics.PrintServiceObjectCount(ServiceName, ref EntityCount, _list.Count);
        }

        private static void TickInternal(MovementComponent movementComponent)
        {
            try
            {
                if (Diagnostics.CheckServiceObjectCount)
                    Interlocked.Increment(ref Instance.EntityCount);

                long startTick = MonotonicTime.NowMs;
                NavQueryProfile.Reset();
                movementComponent.Tick();
                long stopTick = MonotonicTime.NowMs;

                // With what it asked of the navigation mesh and where it was going: nine in ten of the slow
                // turns of two days of play (1,460 of 1,631) were groups walking in Emain Macha, and the line named nobody's reason.
                if (stopTick - startTick > Diagnostics.LongTickThreshold)
                    log.Warn($"Long {Instance.ServiceName}.{nameof(Tick)} for {movementComponent.Owner.Name}({movementComponent.Owner.ObjectID}) Time: {stopTick - startTick}ms{NavQueryProfile.Summary()}" +
                             $" region={movementComponent.Owner.CurrentRegionID} at={movementComponent.Owner.X},{movementComponent.Owner.Y},{movementComponent.Owner.Z}" +
                             $"{(movementComponent is NpcMovementComponent mover ? " " + mover.DescribeRouteState() : string.Empty)}");
            }
            catch (Exception e)
            {
                GameServiceUtils.HandleServiceException(e, Instance.ServiceName, movementComponent, movementComponent.Owner);
            }
        }
    }
}
