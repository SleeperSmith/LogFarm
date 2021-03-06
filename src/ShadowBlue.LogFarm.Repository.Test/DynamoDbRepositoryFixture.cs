﻿using System;
using System.Linq;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Runtime;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using ShadowBlue.LogFarm.Base;
using ShadowBlue.LogFarm.Base.Properties;
using ShadowBlue.LogFarm.Repository.Models;
using Should.Fluent;

namespace ShadowBlue.LogFarm.Repository.Test
{
    [TestFixture(Category = LogFarmApplication.TestCategories.Integration)]
    public class DynamoDbRepositoryFixture
    {
        private static readonly DynamoDBOperationConfig DefaultDbOperationConfig
            = new DynamoDBOperationConfig
            {
                OverrideTableName = Settings.Default.ElmahTableName,
                SkipVersionCheck = true,
                IndexName = "ApplicationName-DateTimeId-Index"
            };

        public DynamoDBContext InitialiseClient()
        {
            try
            {
                var cloudwatchClient = new DynamoDBContext(RegionEndpoint.USWest1);

                return cloudwatchClient;
            }
            catch (AmazonServiceException)
            {
                var lines = System.IO.File.ReadAllLines(@"..\..\..\TestArtifacts\credentials.dec");

                var key = lines.ElementAt(1);
                var secret = lines.ElementAt(2);

                try
                {
                    var cloudwatchClient = new DynamoDBContext(new AmazonDynamoDBClient(new BasicAWSCredentials(key, secret), RegionEndpoint.USWest1), DefaultDbOperationConfig);

                    return cloudwatchClient;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("The security token included in the request is invalid key"))
                        throw new Exception(string.Format("The security token included in the request is invalid key {0}", key));
                    throw;
                }
            }
        }

        [SetUp]
        public void SetupPerTest()
        {
            var ddb = InitialiseClient();

            var removeTargets = ddb.FromScan<ElmahError>(
                new ScanOperationConfig(), DefaultDbOperationConfig).ToList();
            var removeAllWrite = ddb.CreateBatchWrite<ElmahError>(DefaultDbOperationConfig);
            removeAllWrite.AddDeleteItems(removeTargets);
            ddb.ExecuteBatchWrite(removeAllWrite);
        }

        [Test]
        public void RepositoryAdd_Verify()
        {
            //arrange
            var container = new AutoMoq.AutoMoqer();
            var repositoryMock = container.GetMock<IDynamoDBContext>();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance("dummy");

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            var fakeEpochId = string.Format("{0}-{1}", "123", Guid.NewGuid());

            var error = Builder<ElmahError>.CreateNew()
                .With(x => x.ApplicationName = Settings.Default.ApplicationName)
                .With(x => x.DateTimeId = fakeEpochId)
                .Build();

            //act
            target.Add(error);

            //assert
            repositoryMock.Verify(x =>
                    x.Save(It.Is<ElmahError>(err =>
                        error.ApplicationName == Settings.Default.ApplicationName &&
                        error.DateTimeId == fakeEpochId
                    ),
                    It.Is<DynamoDBOperationConfig>(config => config.OverrideTableName == Settings.Default.ElmahTableName)
                ),
                Times.Once()
            );
        }

        [Test]
        public void RepositoryAdd_VerifyAdd()
        {
            //arrange
            var ddb = InitialiseClient();

            var container = new AutoMoq.AutoMoqer();
            var settingMock = container.GetMock<ISettings>();

            container.SetInstance<IDynamoDBContext>(ddb);
            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance("dummy");

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            var fakeEpochId = string.Format("{0}-{1}", "123", Guid.NewGuid());

            var error = Builder<ElmahError>.CreateNew()
                .With(x => x.ApplicationName = Settings.Default.ApplicationName)
                .With(x => x.DateTimeId = fakeEpochId)
                .Build();

            //act
            target.Add(error);

            var results = ddb.FromScan<ElmahError>(
                new ScanOperationConfig(), DefaultDbOperationConfig).ToList();

            //assert
            results.Single().DateTimeId.Should().Equal(fakeEpochId);
        }

        [Test]
        public void RepositoryDelete_Verify()
        {
            //arrange
            var container = new AutoMoq.AutoMoqer();
            var repositoryMock = container.GetMock<IDynamoDBContext>();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

           
            container.SetInstance("dummy");

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            var fakeEpochId = string.Format("{0}-{1}", "123", Guid.NewGuid());

            //act
            target.Delete(fakeEpochId);

            //assert
            repositoryMock.Verify(x =>
                    x.Delete<ElmahError>(
                        It.Is<string>(err =>
                            err == fakeEpochId
                        ),
                        It.Is<string>(asd =>
                            asd == Settings.Default.ApplicationName
                        ),
                        It.Is<DynamoDBOperationConfig>(config => 
                            config.OverrideTableName == Settings.Default.ElmahTableName)
                    ),
                Times.Once()
            );
        }

        [Test]
        public void RepositoryDelete_VerifyDelete()
        {
            //arrange
            var ddb = InitialiseClient();

            var container = new AutoMoq.AutoMoqer();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance<IDynamoDBContext>(ddb);
            container.SetInstance("dummy");

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            var fakeEpochId = string.Format("{0}-{1}", "123", Guid.NewGuid());

            var error = Builder<ElmahError>.CreateNew()
                .With(x => x.ApplicationName = Settings.Default.ApplicationName)
                .With(x => x.DateTimeId = fakeEpochId)
                .Build();

            ddb.Save(error, DefaultDbOperationConfig);

            //act
            target.Delete(fakeEpochId);

            var results = ddb.FromScan<ElmahError>(
                new ScanOperationConfig(), DefaultDbOperationConfig).ToList();

            //assert
            results.Count().Should().Be.LessThanOrEqualTo(0);
        }

        [Test]
        public void RepositoryGet_Verify()
        {
            var container = new AutoMoq.AutoMoqer();
            var repositoryMock = container.GetMock<IDynamoDBContext>();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance("dummy");

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            var fakeEpochId = string.Format("{0}-{1}", "123", Guid.NewGuid());

            target.Get(fakeEpochId);

            repositoryMock.Verify(x =>
                    x.Load<ElmahError>(
                        It.Is<string>(err =>
                            err == fakeEpochId
                        ),
                         It.Is<string>(err =>
                            err == Settings.Default.ApplicationName
                        ),
                        It.Is<DynamoDBOperationConfig>(config => 
                            config.OverrideTableName == Settings.Default.ElmahTableName
                        )
                ),
                Times.Once()
            );
        }

        [Test]
        public void RepositoryGet_VerifyGet()
        {
            //arrange
            var ddb = InitialiseClient();

            var container = new AutoMoq.AutoMoqer();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance("dummy");
            container.SetInstance<IDynamoDBContext>(ddb);

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            var fakeEpochId = string.Format("{0}-{1}", "123", Guid.NewGuid());

            var error = Builder<ElmahError>.CreateNew()
                .With(x => x.ApplicationName = Settings.Default.ApplicationName)
                .With(x => x.DateTimeId = fakeEpochId)
                .Build();

            ddb.Save(error, DefaultDbOperationConfig);

            //act
            var result = target.Get(fakeEpochId);

            //assert
            result.DateTimeId.Should().Equal(fakeEpochId);
        }

        [Test]
        public void RepositoryGetAll_Verify()
        {
            //arrange
            var container = new AutoMoq.AutoMoqer();
            var repositoryMock = container.GetMock<IDynamoDBContext>();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance("dummy");

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            //act
            target.GetAll();

            //assert
            repositoryMock.Verify(x =>
                    x.FromScan<ElmahError>(
                        It.IsAny<ScanOperationConfig>(),
                        It.Is<DynamoDBOperationConfig>(config =>
                            config.OverrideTableName == Settings.Default.ElmahTableName
                        )
                ),
                Times.Once()
            );
        }

        [Test]
        public void RepositoryGetAll_VerifyGetAll()
        {
            //arrange
            var ddb = InitialiseClient();

            var container = new AutoMoq.AutoMoqer();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance("dummy");
            container.SetInstance<IDynamoDBContext>(ddb);

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            var errors = Builder<ElmahError>.CreateListOfSize(3)
                .All()
                .With(x => x.ApplicationName = Settings.Default.ApplicationName)
                .Build();

            foreach (var error in errors)
            {
                ddb.Save(error, DefaultDbOperationConfig);
            }

            //act
            var results = target.GetAll();

            //assert
            results.Count().Should().Equal(3);
        }

        [Test]
        public void RepositoryGetAllWithQuery_Verify()
        {
            //arrange
            var container = new AutoMoq.AutoMoqer();
            var repositoryMock = container.GetMock<IDynamoDBContext>();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance("dummy");

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            //act
            target.GetAllWithQuery(ScanOperator.GreaterThan, It.IsAny<ConditionalOperatorValues>(), It.IsAny<object>());

            //assert
            repositoryMock.Verify(x =>
                    x.Query<ElmahError>(
                        It.Is<string>(y => y == Settings.Default.ApplicationName) ,
                        It.Is<DynamoDBOperationConfig>(config =>
                            config.OverrideTableName == Settings.Default.ElmahTableName &&
                            config.QueryFilter.Single().Operator == ScanOperator.GreaterThan &&
                            config.QueryFilter.Single().PropertyName == "DateTimeId"
                        )
                ),
                Times.Once()
            );
        }

        [Test]
        public void RepositoryGetAllWithQuery_VerifyGetAllWithQuery()
        {
            //arrange
            var ddb = InitialiseClient();

            var container = new AutoMoq.AutoMoqer();
            var settingMock = container.GetMock<ISettings>();

            settingMock.Setup(x => x.ApplicationName)
                .Returns(Settings.Default.ApplicationName);
            settingMock.Setup(x => x.ElmahTableName)
                .Returns(Settings.Default.ElmahTableName);

            container.SetInstance("dummy");
            container.SetInstance<IDynamoDBContext>(ddb);

            var target = container.Resolve<DynamoDbRepository<ElmahError>>();

            var errors = Builder<ElmahError>.CreateListOfSize(3)
                .All()
                .With(x => x.ApplicationName = Settings.Default.ApplicationName)
                .Build();

            var i = 0;
            foreach (var error in errors)
            {
                error.DateTimeId = i + error.DateTimeId;
                ddb.Save(error, DefaultDbOperationConfig);

                i++;
            }

            //act
            var results = target.GetAllWithQuery(ScanOperator.GreaterThan, It.IsAny<ConditionalOperatorValues>(), "1");

            //assert
            results.Count().Should().Equal(2);
        }
    }
}
