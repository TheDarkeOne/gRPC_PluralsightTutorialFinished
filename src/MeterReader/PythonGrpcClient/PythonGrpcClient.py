import grpc

from google.protobuf.timestamp_pb2 import Timestamp

import enums_pb2 as Enums
import MeterReader_pb2 as MeterReader
import MeterReader_pb2_grpc as MeterReaderService
def main():
        print("Calling gRPC Service")

        with open("localhost.cer", "rb") as file:
            cert = file.read()

        credentials = grpc.ssl_channel_credentials(cert)
        
        channel = grpc.secure_channel("localhost:5001", credentials)
        stub = MeterReaderService.MeterReadingServiceStub(channel)

        request = MeterReader.ReadingPacket(successful = Enums.ReadingStatus.SUCCESS)
        reading = MeterReader.ReadingMessage(customerId = 1, readingValue = 1, readingTime = Timestamp().GetCurrentTime())

        request.readings.append(reading)

        result = stub.AddReading(request)
        if ( result.success == Enums.ReadingStatus.SUCCESS):
            print("Success")
        else:
            print("Failure")

main()