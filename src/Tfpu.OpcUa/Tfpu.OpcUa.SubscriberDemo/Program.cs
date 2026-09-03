using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Opc.Ua;
using Opc.Ua.PubSub;
using Tfpu.OpcUa.SubscriberDemo;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<PublisherApplicationOptions>(builder.Configuration.GetSection(PublisherApplicationOptions.SectionName));
using var host = builder.Build();

const int maxNodesPerDataSet = 250;

var options = host.Services.GetRequiredService<IOptions<PublisherApplicationOptions>>().Value;
var configuration = CreateConfiguration(options);

var telemetry = DefaultTelemetry.Create(logging =>
{
    logging.SetMinimumLevel(LogLevel.Warning);
    logging.AddConsole();
});

using var application = UaPubSubApplication.Create(configuration, telemetry);
application.DataReceived += OnDataReceived;
//application.RawDataReceived += OnRawDataReceived;
application.Start();

Console.WriteLine($"Subscriber listening on {options.PublisherUrl}");
Console.WriteLine("Press ENTER to stop.");
Console.ReadLine();

application.Stop();

PubSubConfigurationDataType CreateConfiguration(PublisherApplicationOptions options)
{
    var connection = new PubSubConnectionDataType
    {
        Name = "Subscriber",
        Enabled = true,
        PublisherId = (ushort)1,
        TransportProfileUri = Profiles.PubSubUdpUadpTransport,
        Address = new ExtensionObject(
            new NetworkAddressUrlDataType
            {
                NetworkInterface = options.NetworkInterface,
                Url = options.PublisherUrl
            })
    };

    var readerGroup = new ReaderGroupDataType
    {
        Name = "ReaderGroup",
        Enabled = true,
        MaxNetworkMessageSize = 65507        
    };


    ushort writerGroupId = 1;
    foreach (var plc in options.Plcs)
    {
        var batchIndex = 1;
        foreach (var nodesBatch in plc.Nodes.Chunk(maxNodesPerDataSet))
        {
            var dataSetName = $"{plc.Name}_Batch{batchIndex}";

            var metadata = new DataSetMetaDataType
            {
                Name = dataSetName,
                DataSetClassId = Uuid.Empty,
                ConfigurationVersion = new ConfigurationVersionDataType
                {
                    MajorVersion = 1,
                    MinorVersion = 1
                }
            };

            foreach (var node in nodesBatch)
            {
                metadata.Fields.Add(
                    new FieldMetaData
                    {
                        Name = node.Name,
                        DataType = node.DataType.ToOpcUaDataType(),
                        BuiltInType = (byte)node.DataType.ToOpcUaBuiltInType(),
                        ValueRank = ValueRanks.Scalar
                    });
            }

            var dataSetReader = new DataSetReaderDataType
            {
                Name = $"{dataSetName}_DataSetReader",
                Enabled = true,

                PublisherId = (ushort)1,
                WriterGroupId = writerGroupId,
                DataSetWriterId = writerGroupId,

                DataSetMetaData = metadata,
                DataSetFieldContentMask = (uint)DataSetFieldContentMask.None,
                KeyFrameCount = 1
            };

            dataSetReader.MessageSettings = new ExtensionObject(
                new UadpDataSetReaderMessageDataType
                {
                    NetworkMessageContentMask = (uint)(
                        UadpNetworkMessageContentMask.PublisherId |
                        UadpNetworkMessageContentMask.GroupHeader |
                        UadpNetworkMessageContentMask.PayloadHeader |
                        UadpNetworkMessageContentMask.WriterGroupId |
                        UadpNetworkMessageContentMask.SequenceNumber),

                    DataSetMessageContentMask = (uint)(
                        UadpDataSetMessageContentMask.Status |
                        UadpDataSetMessageContentMask.SequenceNumber)
                });

            readerGroup.DataSetReaders.Add(dataSetReader);

            writerGroupId++;
            batchIndex++;
        }
    }

    connection.ReaderGroups.Add(readerGroup);

    return new PubSubConfigurationDataType
    {
        Enabled = true,
        Connections = { connection }
    };
}

void OnDataReceived(object? sender, SubscribedDataEventArgs e)
{
    foreach (var message in e.NetworkMessage.DataSetMessages)
    {
        var dataSet = message.DataSet;

        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {dataSet.Name} - "
            + $"{string.Join(", ", dataSet.Fields.Take(5).Select(f => $"{f.FieldMetaData?.Name}: {f.Value?.WrappedValue.Value}"))}");
    }
}