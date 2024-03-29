﻿using NServiceBus.Features;
using NServiceBus.Pipeline;
using System;
using System.Collections.Generic;
using Microsoft.ServiceFabric.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NServiceBus.ServiceFabric.Persistence
{
    public class ServiceFabricSharedTransaction : Feature
    {
        internal ServiceFabricSharedTransaction()
        {
            DependsOnAtLeastOne(typeof(ServiceFabricSagaPersistence), typeof(ServiceFabricOutboxPersistence));
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            var stateManager = context.Settings.Get("ServiceFabricStateManager") as IReliableStateManager;

            if (stateManager == null)
                throw new Exception("Service Fabric state manager has not been set");

            context.Pipeline.Register<OpenServiceFabricSharedTransactionBehavior.Registration>();
            context.Container.ConfigureComponent(b => new ServiceFabricStorageContext(b.Build<PipelineExecutor>(), b.Build<IReliableStateManager>()), DependencyLifecycle.InstancePerUnitOfWork);


            context.Container.RegisterSingleton(stateManager);
        }
    }
}
