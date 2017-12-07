using System;
using System.Threading.Tasks;

namespace Meteor.ddp
{
    public class WebSocketConnection
    {
        protected DdpConnection ddpConnection;

        async public virtual Task Connect(Uri uri)
        {
            throw new NotImplementedException();
        }

        public virtual void Dispose()
        {
            throw new NotImplementedException();
        }

        async public virtual Task Send(string message)
        {
            throw new NotImplementedException();
        }
    }
}