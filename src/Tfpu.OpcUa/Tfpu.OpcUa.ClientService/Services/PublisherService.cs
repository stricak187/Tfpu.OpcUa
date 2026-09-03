using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.PubSub;
using Tfpu.OpcUa.ClientService.Models;

namespace Tfpu.OpcUa.ClientService.Services;

public class PublisherService : IDisposable
{
    private readonly ILogger<PublisherService> _logger;
    private readonly ClientApplicationOptions _clientApplicationOptions;
    private readonly PubSubConfigurationDataType _pubSubConfigurationDataType;
    private readonly ITelemetryContext _telemetry;
    private readonly UaPubSubApplication _application;
    private bool _isRunning;

    private readonly object _lifecycleLock = new();

    private const int maxNodesPerDataSet = 250;

    public PublisherService(
        ILogger<PublisherService> logger,
        IOptions<ClientApplicationOptions> clientApplicationOptions)
    {
        _logger = logger;
        _clientApplicationOptions = clientApplicationOptions.Value;
        _pubSubConfigurationDataType = CreateConfiguration();
        _telemetry = DefaultTelemetry.Create(logging => {
            logging.SetMinimumLevel(LogLevel.Warning);
            logging.AddConsole();
        });
        _application = UaPubSubApplication.Create(_pubSubConfigurationDataType, _telemetry);
    }

    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                _application.Start();
                _isRunning = true;

                _logger.LogInformation("OPC UA PubSub publisher started.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while starting the publisher application.");

                throw;
            }
        }
    }

    public void PublishValue(NodeId nodeId, DataValue value)
    {
        try
        {
            _application.DataStore.WritePublishedDataItem(nodeId, Attributes.Value, value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while publishing value.");
        }
    }

    public void Stop()
    {
        lock (_lifecycleLock)
        {
            if (!_isRunning)
            {
                return;
            }

            try
            {
                _application.Stop();
                _isRunning = false;

                _logger.LogInformation("OPC UA PubSub publisher stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while stopping the publisher application.");
                throw;
            }
        }
    }
    public PublisherSnapshot GetSnapshot()
    {
        lock (_lifecycleLock)
        {
            return new PublisherSnapshot(
                IsRunning: _isRunning,
                NodeCount: _clientApplicationOptions.Plcs.Sum(plc => plc.Nodes.Count),
                DataSetCount: _pubSubConfigurationDataType.PublishedDataSets.Count,
                Transport: "UDP/UADP",
                Url: _clientApplicationOptions.PublisherUrl);
        }
    }

    public void Dispose()
    {
        _application.Dispose();
    }

    private PubSubConfigurationDataType CreateConfiguration()
    {
        var connection = new PubSubConnectionDataType
        {
            Name = $"{_clientApplicationOptions.ApplicationName}.Publisher",
            Enabled = true,
            PublisherId = (ushort)1,
            TransportProfileUri = Profiles.PubSubUdpUadpTransport,
            Address = new ExtensionObject(
                new NetworkAddressUrlDataType
                {
                    NetworkInterface = _clientApplicationOptions.NetworkInterface,
                    Url = _clientApplicationOptions.PublisherUrl
                })
        };

        var configuration = new PubSubConfigurationDataType();

        ushort writerGroupId = 1;
        foreach (var plc in _clientApplicationOptions.Plcs)
        {
            var batchIndex = 1;

            foreach (var nodesBatch in plc.Nodes.Chunk(maxNodesPerDataSet))
            {
                var dataSetName = $"{plc.Name}_Batch{batchIndex}";

                var publishedDataSet = new PublishedDataSetDataType { Name = dataSetName };
                var publishedDataItems = new PublishedDataItemsDataType();

                foreach (var node in nodesBatch)
                {
                    publishedDataSet.DataSetMetaData.Fields.Add(
                        new FieldMetaData
                        {
                            Name = node.Name,
                            DataType = node.DataType.ToOpcUaDataType(),
                            BuiltInType = (byte)node.DataType.ToOpcUaBuiltInType(),
                            ValueRank = ValueRanks.Scalar,
                            DataSetFieldId = new Uuid(Guid.NewGuid())
                        });

                    publishedDataItems.PublishedData.Add(
                        new PublishedVariableDataType
                        {
                            PublishedVariable = new NodeId(node.Address, node.NamespaceIndex),
                            AttributeId = Attributes.Value
                        });
                }

                publishedDataSet.DataSetSource = new ExtensionObject(publishedDataItems);

                var writerGroup = new WriterGroupDataType
                {
                    Name = $"{dataSetName}_WriterGroup",
                    Enabled = true,
                    WriterGroupId = writerGroupId,
                    PublishingInterval = 500,
                    KeepAliveTime = 15 * 1000,
                    MaxNetworkMessageSize = 65507
                };

                writerGroup.MessageSettings = new ExtensionObject(
                    new UadpWriterGroupMessageDataType
                    {
                        DataSetOrdering = DataSetOrderingType.AscendingWriterId,

                        NetworkMessageContentMask = (uint)(
                            UadpNetworkMessageContentMask.PublisherId |
                            UadpNetworkMessageContentMask.GroupHeader |
                            UadpNetworkMessageContentMask.PayloadHeader |
                            UadpNetworkMessageContentMask.WriterGroupId |
                            UadpNetworkMessageContentMask.SequenceNumber)
                    });

                writerGroup.TransportSettings = new ExtensionObject(new DatagramWriterGroupTransportDataType());

                var dataSetWriter = new DataSetWriterDataType
                {
                    Name = $"{dataSetName}_DataSetWriter",
                    Enabled = true,
                    DataSetWriterId = writerGroupId,
                    DataSetName = dataSetName,
                    KeyFrameCount = 1
                };

                dataSetWriter.MessageSettings = new ExtensionObject(
                    new UadpDataSetWriterMessageDataType
                    {
                        NetworkMessageNumber = 1,
                        DataSetMessageContentMask = (uint)(UadpDataSetMessageContentMask.Status | UadpDataSetMessageContentMask.SequenceNumber)
                    });

                writerGroup.DataSetWriters.Add(dataSetWriter);
                connection.WriterGroups.Add(writerGroup);
                configuration.PublishedDataSets.Add(publishedDataSet);

                writerGroupId++;
                batchIndex++;
            }
        }

        configuration.Connections.Add(connection);
        return configuration;
    }
}