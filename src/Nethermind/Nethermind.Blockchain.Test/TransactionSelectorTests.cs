//  Copyright (c) 2021 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Nethermind.Blockchain.Comparers;
using Nethermind.Blockchain.Producers;
using Nethermind.Consensus;
using Nethermind.Consensus.Transactions;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Core.Specs;
using Nethermind.Specs.Forks;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using Nethermind.TxPool;
using NSubstitute;
using NUnit.Framework;

namespace Nethermind.Blockchain.Test
{
    public class TransactionSelectorTests
    {
        public static IEnumerable ProperTransactionsSelectedTestCases
        {
            get
            {
                ProperTransactionsSelectedTestCase allTransactionsSelected = ProperTransactionsSelectedTestCase.Default;
                allTransactionsSelected.ExpectedSelectedTransactions.AddRange(
                    allTransactionsSelected.WrappedTransactions.OrderBy(t => t.Tx.Nonce));
                yield return new TestCaseData(allTransactionsSelected).SetName("All transactions selected");

                ProperTransactionsSelectedTestCase noneTransactionSelectedDueToValue =
                    ProperTransactionsSelectedTestCase.Default;
                noneTransactionSelectedDueToValue.WrappedTransactions.ForEach(t => t.Tx.Value = 901);
                yield return new TestCaseData(noneTransactionSelectedDueToValue).SetName(
                    "None transactions selected due to value");

                ProperTransactionsSelectedTestCase noneTransactionsSelectedDueToGasPrice =
                    ProperTransactionsSelectedTestCase.Default;
                noneTransactionsSelectedDueToGasPrice.WrappedTransactions.ForEach(t => t.Tx.GasPrice = 100);
                yield return new TestCaseData(noneTransactionsSelectedDueToGasPrice).SetName(
                    "None transactions selected due to transaction gas price and limit");

                ProperTransactionsSelectedTestCase noneTransactionsSelectedDueToGasLimit =
                    ProperTransactionsSelectedTestCase.Default;
                noneTransactionsSelectedDueToGasLimit.GasLimit = 9;
                yield return new TestCaseData(noneTransactionsSelectedDueToGasLimit).SetName(
                    "None transactions selected due to gas limit");

                ProperTransactionsSelectedTestCase oneTransactionSelectedDueToValue =
                    ProperTransactionsSelectedTestCase.Default;
                oneTransactionSelectedDueToValue.WrappedTransactions.ForEach(t => t.Tx.Value = 500);
                oneTransactionSelectedDueToValue.ExpectedSelectedTransactions.AddRange(oneTransactionSelectedDueToValue
                    .WrappedTransactions.OrderBy(t => t.Tx.Nonce).Take(1));
                yield return new TestCaseData(oneTransactionSelectedDueToValue).SetName(
                    "One transaction selected due to gas limit and value");

                ProperTransactionsSelectedTestCase twoTransactionSelectedDueToValue =
                    ProperTransactionsSelectedTestCase.Default;
                twoTransactionSelectedDueToValue.WrappedTransactions.ForEach(t => t.Tx.Value = 400);
                twoTransactionSelectedDueToValue.ExpectedSelectedTransactions.AddRange(twoTransactionSelectedDueToValue
                    .WrappedTransactions.OrderBy(t => t.Tx.Nonce).Take(2));
                yield return new TestCaseData(twoTransactionSelectedDueToValue).SetName(
                    "Two transaction selected due to gas limit and value");

                ProperTransactionsSelectedTestCase twoTransactionSelectedDueToMinGasPriceForMining =
                    ProperTransactionsSelectedTestCase.Default;
                twoTransactionSelectedDueToMinGasPriceForMining.MinGasPriceForMining = 2;
                twoTransactionSelectedDueToMinGasPriceForMining.ExpectedSelectedTransactions.AddRange(
                    twoTransactionSelectedDueToValue.WrappedTransactions.OrderBy(t => t.Tx.Nonce).Take(2));
                yield return new TestCaseData(twoTransactionSelectedDueToValue).SetName(
                    "Two transaction selected due to min gas price for mining");

                ProperTransactionsSelectedTestCase twoTransactionSelectedDueToWrongNonce =
                    ProperTransactionsSelectedTestCase.Default;
                twoTransactionSelectedDueToWrongNonce.WrappedTransactions.First().Tx.Nonce = 4;
                twoTransactionSelectedDueToWrongNonce.ExpectedSelectedTransactions.AddRange(
                    twoTransactionSelectedDueToWrongNonce.WrappedTransactions.OrderBy(t => t.Tx.Nonce).Take(2));
                yield return new TestCaseData(twoTransactionSelectedDueToWrongNonce).SetName(
                    "Two transaction selected due to wrong nonce");

                ProperTransactionsSelectedTestCase twoTransactionSelectedDueToLackOfSenderAddress =
                    ProperTransactionsSelectedTestCase.Default;
                twoTransactionSelectedDueToLackOfSenderAddress.WrappedTransactions.First().Tx.SenderAddress = null;
                twoTransactionSelectedDueToLackOfSenderAddress.ExpectedSelectedTransactions.AddRange(
                    twoTransactionSelectedDueToLackOfSenderAddress.WrappedTransactions.OrderBy(t => t.Tx.Nonce).Take(2));
                yield return new TestCaseData(twoTransactionSelectedDueToLackOfSenderAddress).SetName(
                    "Two transaction selected due to lack of sender address");

                ProperTransactionsSelectedTestCase missingAddressState = ProperTransactionsSelectedTestCase.Default;
                missingAddressState.MissingAddresses.Add(TestItem.AddressA);
                yield return new TestCaseData(missingAddressState).SetName("Missing address state");

                ProperTransactionsSelectedTestCase complexCase = new ProperTransactionsSelectedTestCase()
                {
                    AccountStates =
                    {
                        {TestItem.AddressA, (1000, 1)},
                        {TestItem.AddressB, (1000, 0)},
                        {TestItem.AddressC, (1000, 3)}
                    },
                    WrappedTransactions =
                    {
                        // A
                        /*0*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(1).WithValue(10)
                            .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                        /*1*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(3).WithValue(1)
                            .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                        /*2*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(2).WithValue(10)
                            .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),

                        //B
                        /*3*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressB).WithNonce(0).WithValue(1)
                            .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyB).TestObject),
                        /*4*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressB).WithNonce(1).WithValue(1)
                            .WithGasPrice(10).WithGasLimit(9).SignedAndResolved(TestItem.PrivateKeyB).TestObject),
                        /*5*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressB).WithNonce(3).WithValue(1)
                            .WithGasPrice(10).WithGasLimit(9).SignedAndResolved(TestItem.PrivateKeyB).TestObject),

                        //C
                        /*6*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressC).WithNonce(3).WithValue(500)
                            .WithGasPrice(19).WithGasLimit(9).SignedAndResolved(TestItem.PrivateKeyC).TestObject),
                        /*7*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressC).WithNonce(3).WithValue(500)
                            .WithGasPrice(20).WithGasLimit(9).SignedAndResolved(TestItem.PrivateKeyC).TestObject),
                        /*8*/
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressC).WithNonce(4).WithValue(500)
                            .WithGasPrice(20).WithGasLimit(9).SignedAndResolved(TestItem.PrivateKeyC).TestObject),
                    },
                    GasLimit = 10000000
                };
                complexCase.ExpectedSelectedTransactions.AddRange(
                    new[] {7, 3, 4, 0, 2, 1}.Select(i => complexCase.WrappedTransactions[i]));
                yield return new TestCaseData(complexCase).SetName("Complex case");
            }
        }

        public static IEnumerable Eip1559LegacyTransactionTestCases
        {
            get
            {
                ProperTransactionsSelectedTestCase allTransactionsSelected = ProperTransactionsSelectedTestCase.Eip1559DefaultLegacyTransactions;
                allTransactionsSelected.BaseFee = 0;
                allTransactionsSelected.ExpectedSelectedTransactions.AddRange(
                    allTransactionsSelected.WrappedTransactions.OrderBy(t => t.Tx.Nonce));
                yield return new TestCaseData(allTransactionsSelected).SetName("Legacy transactions: All transactions selected - 0 BaseFee");
                
                ProperTransactionsSelectedTestCase baseFeeLowerThanGasPrice = ProperTransactionsSelectedTestCase.Eip1559DefaultLegacyTransactions;
                baseFeeLowerThanGasPrice.BaseFee = 5;
                baseFeeLowerThanGasPrice.ExpectedSelectedTransactions.AddRange(
                    baseFeeLowerThanGasPrice.WrappedTransactions.OrderBy(t => t.Tx.Nonce));
                yield return new TestCaseData(baseFeeLowerThanGasPrice).SetName("Legacy transactions: All transactions selected - BaseFee lower than gas price");
                
                ProperTransactionsSelectedTestCase baseFeeGreaterThanGasPrice = ProperTransactionsSelectedTestCase.Eip1559DefaultLegacyTransactions;
                baseFeeGreaterThanGasPrice.BaseFee = 1.GWei();
                yield return new TestCaseData(baseFeeGreaterThanGasPrice).SetName("Legacy transactions: None transactions selected - BaseFee greater than gas price");
                
                ProperTransactionsSelectedTestCase baseFeeBalanceCheck = new ProperTransactionsSelectedTestCase()
                {
                    Eip1559Enabled = true,
                    BaseFee = 5,
                    AccountStates = {{TestItem.AddressA, (1000, 1)}},
                    WrappedTransactions =
                    {
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(3)
                            .WithGasPrice(60).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                            new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(1)
                            .WithGasPrice(30).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                                new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(2)
                            .WithGasPrice(20).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject)
                    },
                    GasLimit = 10000000
                };
                baseFeeBalanceCheck.ExpectedSelectedTransactions.AddRange(
                    new[] {1, 2 }.Select(i => baseFeeBalanceCheck.WrappedTransactions[i]));
                yield return new TestCaseData(baseFeeBalanceCheck).SetName("Legacy transactions: two transactions selected because of account balance");
                
                ProperTransactionsSelectedTestCase balanceCheckWithTxValue = new ProperTransactionsSelectedTestCase()
                {
                    Eip1559Enabled = true,
                    BaseFee = 5,
                    AccountStates = {{TestItem.AddressA, (300, 1)}},
                    WrappedTransactions =
                    {
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(2)
                            .WithGasPrice(5).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                            new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(1)
                            .WithGasPrice(20).WithGasLimit(10).WithValue(100).SignedAndResolved(TestItem.PrivateKeyA).TestObject)
                    },
                    GasLimit = 10000000
                };
                balanceCheckWithTxValue.ExpectedSelectedTransactions.AddRange(
                    new[] {1 }.Select(i => balanceCheckWithTxValue.WrappedTransactions[i]));
                yield return new TestCaseData(balanceCheckWithTxValue).SetName("Legacy transactions: one transaction selected because of account balance");
            }
        }
        
        public static IEnumerable Eip1559TestCases
        {
            get
            {
                ProperTransactionsSelectedTestCase allTransactionsSelected = ProperTransactionsSelectedTestCase.Eip1559Default;
                allTransactionsSelected.BaseFee = 0;
                allTransactionsSelected.ExpectedSelectedTransactions.AddRange(
                    allTransactionsSelected.WrappedTransactions.OrderBy(t => t.Tx.Nonce));
                yield return new TestCaseData(allTransactionsSelected).SetName("EIP1559 transactions: All transactions selected - 0 BaseFee");
                
                ProperTransactionsSelectedTestCase baseFeeLowerThanGasPrice = ProperTransactionsSelectedTestCase.Eip1559Default;
                baseFeeLowerThanGasPrice.BaseFee = 5;
                baseFeeLowerThanGasPrice.ExpectedSelectedTransactions.AddRange(
                    baseFeeLowerThanGasPrice.WrappedTransactions.OrderBy(t => t.Tx.Nonce));
                yield return new TestCaseData(baseFeeLowerThanGasPrice).SetName("EIP1559 transactions: All transactions selected - BaseFee lower than gas price");
                
                ProperTransactionsSelectedTestCase baseFeeGreaterThanGasPrice = ProperTransactionsSelectedTestCase.Eip1559Default;
                baseFeeGreaterThanGasPrice.BaseFee = 1.GWei();
                yield return new TestCaseData(baseFeeGreaterThanGasPrice).SetName("EIP1559 transactions: None transactions selected - BaseFee greater than gas price");
                
                ProperTransactionsSelectedTestCase balanceCheckWithTxValue = new ProperTransactionsSelectedTestCase()
                {
                    Eip1559Enabled = true,
                    BaseFee = 5,
                    AccountStates = {{TestItem.AddressA, (400, 1)}},
                    WrappedTransactions =
                    {
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithType(TxType.EIP1559).WithNonce(2)
                            .WithFeeCap(4).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                            new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithType(TxType.EIP1559).WithNonce(1)
                            .WithFeeCap(50).WithGasLimit(10).WithValue(100).SignedAndResolved(TestItem.PrivateKeyA).TestObject)
                    },
                    GasLimit = 10000000
                };
                balanceCheckWithTxValue.ExpectedSelectedTransactions.AddRange(
                    new[] { 1 }.Select(i => balanceCheckWithTxValue.WrappedTransactions[i]));
                yield return new TestCaseData(balanceCheckWithTxValue).SetName("EIP1559 transactions: one transaction selected because of account balance");
                
                ProperTransactionsSelectedTestCase balanceCheckWithGasPremium = new ProperTransactionsSelectedTestCase()
                {
                    Eip1559Enabled = true,
                    BaseFee = 5,
                    AccountStates = {{TestItem.AddressA, (400, 1)}},
                    WrappedTransactions =
                    {
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithType(TxType.EIP1559).WithNonce(2)
                            .WithFeeCap(5).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                            new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(1)
                            .WithFeeCap(50).WithGasPremium(25).WithGasLimit(10).WithType(TxType.EIP1559).WithValue(60).SignedAndResolved(TestItem.PrivateKeyA).TestObject)
                    },
                    GasLimit = 10000000
                };
                balanceCheckWithGasPremium.ExpectedSelectedTransactions.AddRange(
                    new[] { 1 }.Select(i => balanceCheckWithGasPremium.WrappedTransactions[i]));
                yield return new TestCaseData(balanceCheckWithGasPremium).SetName("EIP1559 transactions: one transaction selected because of account balance and miner tip");
            }
        }

        [TestCaseSource(nameof(ProperTransactionsSelectedTestCases))]
        [TestCaseSource(nameof(Eip1559LegacyTransactionTestCases))]
        [TestCaseSource(nameof(Eip1559TestCases))]
        public void Proper_transactions_selected(ProperTransactionsSelectedTestCase testCase)
        {
            MemDb stateDb = new MemDb();
            MemDb codeDb = new MemDb();
            TrieStore trieStore = new TrieStore(stateDb, LimboLogs.Instance);
            StateProvider stateProvider = new StateProvider(trieStore, codeDb, LimboLogs.Instance);
            StateReader stateReader =
                new StateReader(new TrieStore(stateDb, LimboLogs.Instance), codeDb, LimboLogs.Instance);
            ISpecProvider specProvider = Substitute.For<ISpecProvider>();

            void SetAccountStates(IEnumerable<Address> missingAddresses)
            {
                HashSet<Address> missingAddressesSet = missingAddresses.ToHashSet();

                foreach (KeyValuePair<Address, (UInt256 Balance, UInt256 Nonce)> accountState in testCase.AccountStates
                    .Where(v => !missingAddressesSet.Contains(v.Key)))
                {
                    stateProvider.CreateAccount(accountState.Key, accountState.Value.Balance);
                    for (int i = 0; i < accountState.Value.Nonce; i++)
                    {
                        stateProvider.IncrementNonce(accountState.Key);
                    }
                }

                stateProvider.Commit(Homestead.Instance);
                stateProvider.CommitTree(0);
            }

            ITxPool transactionPool = Substitute.For<ITxPool>();
            IBlockTree blockTree = Substitute.For<IBlockTree>();
            Block block = Build.A.Block.WithNumber(0).TestObject;
            blockTree.Head.Returns(block);
            IReleaseSpec spec = new ReleaseSpec()
            {
                IsEip1559Enabled = testCase.Eip1559Enabled
            };
            specProvider.GetSpec(Arg.Any<long>()).Returns(spec);
            TransactionComparerProvider transactionComparerProvider =
                new TransactionComparerProvider(specProvider, blockTree);
            IComparer<WrappedTransaction> defaultComparer = transactionComparerProvider.GetDefaultComparer();
            IComparer<WrappedTransaction> comparer = CompareTxByNonce.Instance.ThenBy(defaultComparer);
            Dictionary<Address?, WrappedTransaction[]> transactions = testCase.WrappedTransactions
                .Where(t => t?.Tx?.SenderAddress != null)
                .GroupBy(t => t.Tx.SenderAddress)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(t => t, comparer).ToArray());
            transactionPool.GetPendingTransactionsBySender().Returns(transactions);
            ITxFilterPipeline txFilterPipeline = new TxFilterPipelineBuilder(LimboLogs.Instance)
                .WithMinGasPriceFilter(testCase.MinGasPriceForMining, specProvider)
                .WithBaseFeeFilter(specProvider)
                .Build;

            SetAccountStates(testCase.MissingAddresses);

            TxPoolTxSource poolTxSource = new TxPoolTxSource(transactionPool, stateReader, specProvider,
                transactionComparerProvider, LimboLogs.Instance, txFilterPipeline);


            IEnumerable<Transaction> selectedTransactions =
                poolTxSource.GetTransactions(Build.A.BlockHeader.WithStateRoot(stateProvider.StateRoot).WithBaseFee(testCase.BaseFee).TestObject,
                    testCase.GasLimit);
            selectedTransactions.Should()
                .BeEquivalentTo(testCase.ExpectedSelectedTransactions.Select(w => w.Tx), o => o.WithStrictOrdering());
        }
    }

    public class ProperTransactionsSelectedTestCase
    {
        public IDictionary<Address, (UInt256 Balance, UInt256 Nonce)> AccountStates { get; } =
            new Dictionary<Address, (UInt256 Balance, UInt256 Nonce)>();
        
        public List<WrappedTransaction> WrappedTransactions { get; } = new List<WrappedTransaction>();

        public long GasLimit { get; set; }
        public List<WrappedTransaction> ExpectedSelectedTransactions { get; } = new List<WrappedTransaction>();
        public UInt256 MinGasPriceForMining { get; set; } = 1;
        
        public bool Eip1559Enabled { get; set; }
        
        public UInt256 BaseFee { get; set; }

        public static ProperTransactionsSelectedTestCase Default =>
            new ProperTransactionsSelectedTestCase()
            {
                AccountStates = {{TestItem.AddressA, (1000, 1)}},
                WrappedTransactions =
                {
                    new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(3).WithValue(1)
                        .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(1).WithValue(10)
                        .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                            new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(2).WithValue(10)
                        .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject)
                },
                GasLimit = 10000000
            };
        
        public static ProperTransactionsSelectedTestCase Eip1559DefaultLegacyTransactions =>
            new ProperTransactionsSelectedTestCase()
            {
                Eip1559Enabled = true,
                BaseFee = 1.GWei(),
                AccountStates = {{TestItem.AddressA, (1000, 1)}},
                WrappedTransactions =
                {
                    new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(3).WithValue(1)
                        .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(1).WithValue(10)
                        .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                            new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithNonce(2).WithValue(10)
                        .WithGasPrice(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject)
                },
                GasLimit = 10000000
            };
        
        public static ProperTransactionsSelectedTestCase Eip1559Default =>
            new ProperTransactionsSelectedTestCase()
            {
                Eip1559Enabled = true,
                BaseFee = 1.GWei(),
                AccountStates = {{TestItem.AddressA, (1000, 1)}},
                WrappedTransactions =
                {
                    new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithType(TxType.EIP1559).WithNonce(3).WithValue(1)
                        .WithFeeCap(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                        new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithType(TxType.EIP1559).WithNonce(1).WithValue(10)
                        .WithFeeCap(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject),
                            new WrappedTransaction(Build.A.Transaction.WithSenderAddress(TestItem.AddressA).WithType(TxType.EIP1559).WithNonce(2).WithValue(10)
                        .WithFeeCap(10).WithGasLimit(10).SignedAndResolved(TestItem.PrivateKeyA).TestObject)
                },
                GasLimit = 10000000
            };

        public List<Address> MissingAddresses { get; } = new List<Address>();
    }
}
