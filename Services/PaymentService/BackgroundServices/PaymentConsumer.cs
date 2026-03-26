namespace PaymentService.BackgroundServices
{
    public class PaymentConsumer : BackgroundService
    {
        private readonly Services.PaymentProcessor _paymentService;

        public PaymentConsumer(Services.PaymentProcessor paymentService)
        {
            _paymentService = paymentService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var evt = await FakeEventBus.Receive<OrderCreatedEvent>();

                if (evt == null)
                {
                    await Task.Delay(500);
                    continue;
                }

                Console.WriteLine("🔥 Payment received OrderCreatedEvent");

                var result = await _paymentService.Process(evt.OrderId, evt.Amount);

                await FakeEventBus.Publish(new PaymentResultEvent
                {
                    OrderId = evt.OrderId,
                    IsSuccess = result
                });
            }
        }
    }
}
