const http = require('http');

const server = http.createServer((req, res) => {
    console.log(`${req.method} ${req.url}`);
    
    if (req.method === 'GET' && req.url === '/status') {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({
            status: 'running',
            server: 'Node.js Test Server',
            timestamp: new Date().toISOString()
        }));
    }
    else if (req.method === 'POST' && req.url === '/emergency') {
        let body = '';
        req.on('data', chunk => body += chunk);
        req.on('end', () => {
            console.log('Emergency received:', body);
            res.writeHead(200, { 'Content-Type': 'application/json' });
            res.end(JSON.stringify({
                status: 'received',
                message: 'Emergency signal processed'
            }));
        });
    }
    else {
        res.writeHead(200, { 'Content-Type': 'text/html' });
        res.end(`
            <html>
            <body>
                <h1>Test Emergency Server (Node.js)</h1>
                <p>Server is running on port 8080</p>
                <p>Endpoints available:</p>
                <ul>
                    <li><strong>POST /emergency</strong> - For ESP-01S emergency</li>
                    <li><strong>GET /status</strong> - Server status check</li>
                </ul>
                <p>Current time: ${new Date().toString()}</p>
            </body>
            </html>
        `);
    }
});

server.listen(8080, () => {
    console.log('Test server running on http://localhost:8080');
    console.log('Ready for ESP-01S connections');
});