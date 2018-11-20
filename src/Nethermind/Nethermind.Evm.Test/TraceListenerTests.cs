﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Evm.Tracing;
using NUnit.Framework;

namespace Nethermind.Evm.Test
{
    [TestFixture]
    public class TraceListenerTests
    {
        [Test]
        public void Starts_with_trace_set_to_null()
        {
            Keccak txHash = TestObject.KeccakA;
            GethLikeBlockTracer gethLikeBlockTracer = new GethLikeBlockTracer(txHash);
            Assert.IsNull(gethLikeBlockTracer.Trace, $"starts with {nameof(gethLikeBlockTracer.Trace)} set to null");
        }
        
        [Test]
        public void Throws_when_recording_unexpected_trace()
        {
            Keccak txHash = TestObject.KeccakA;
            GethLikeBlockTracer gethLikeBlockTracer = new GethLikeBlockTracer(txHash);
            Assert.Throws<InvalidOperationException>(() => gethLikeBlockTracer.RecordTrace(TestObject.KeccakB, new GethLikeTxTrace()));
        }
        
        [Test]
        public void Should_trace_responds_properly()
        {
            Keccak txHash = TestObject.KeccakA;
            GethLikeBlockTracer gethLikeBlockTracer = new GethLikeBlockTracer(txHash);
            Assert.IsTrue(gethLikeBlockTracer.ShouldTrace(new Keccak(txHash.Bytes)));
            Assert.IsFalse(gethLikeBlockTracer.ShouldTrace(TestObject.KeccakB));
        }
        
        [Test]
        public void Records_trace_properly()
        {
            Keccak txHash = TestObject.KeccakA;
            GethLikeBlockTracer gethLikeBlockTracer = new GethLikeBlockTracer(txHash);
            var trace = new GethLikeTxTrace();
            gethLikeBlockTracer.RecordTrace(new Keccak(txHash.Bytes), trace);
            Assert.AreSame(trace, gethLikeBlockTracer.Trace);
        }
    }
}