﻿//-----------------------------------------------------------------------
//  Copyright (c) Microsoft Corporation. All rights reserved.
// 
//      THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY KIND, 
//      EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES 
//      OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
// ----------------------------------------------------------------------------------
//      The example companies, organizations, products, domain names,
//      e-mail addresses, logos, people, places, and events depicted
//      herein are fictitious.  No association with any real company,
//      organization, product, domain name, email address, logo, person,
//      places, or events is intended or should be inferred.
//-----------------------------------------------------------------------

namespace Microsoft.Fabric.Actor.Samples
{
    using System.Runtime.Serialization;

    [DataContract]
    public class TestSpecifications
    {
        [DataMember] 
        public string ApplicationName = "fabric:/PresenceActorApp";

        [DataMember]
        public int NumThreads = 50;

        [DataMember]
        public int NumActorsPerThread = 20;

        [DataMember]
        public int NumOperationsPerActor = 20;

        [DataMember]
        public int NumOutstandingOperationsPerThread = 100;

        [DataMember]
        public double ReadToWriteRatio = 0.7;
    }
}