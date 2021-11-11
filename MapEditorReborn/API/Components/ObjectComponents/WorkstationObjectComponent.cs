﻿namespace MapEditorReborn.API
{
    using Exiled.API.Enums;
    using InventorySystem.Items.Firearms.Attachments;

    /// <summary>
    /// Component added to spawned WorkstationObject. Is is used for easier idendification of the object and it's variables.
    /// </summary>
    public class WorkStationObjectComponent : MapEditorObject
    {
        /// <summary>
        /// Initializes the <see cref="WorkStationObjectComponent"/>.
        /// </summary>
        /// <param name="workStationObject">The <see cref="WorkStationObject"/> to instantiate.</param>
        /// <returns>Instance of this compoment.</returns>
        public WorkStationObjectComponent Init(WorkStationObject workStationObject)
        {
            Base = workStationObject;
            workstationController = GetComponent<WorkstationController>();

            ForcedRoomType = workStationObject.RoomType != RoomType.Unknown ? workStationObject.RoomType : FindRoom().Type;

            UpdateObject();

            return this;
        }

        /// <inheritdoc cref="UpdateObject()"/>
        public override void UpdateObject()
        {
            workstationController.NetworkStatus = (byte)(Base.IsInteractable ? 0 : 4);

            base.UpdateObject();
        }

        /// <summary>
        /// The config-base of the object containing all of it's properties.
        /// </summary>
        public WorkStationObject Base;

        private WorkstationController workstationController;
    }
}
