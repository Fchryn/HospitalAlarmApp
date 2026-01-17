from http.server import HTTPServer, BaseHTTPRequestHandler
import json
import time

class TestHandler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == '/status':
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            response = {
                "status": "running",
                "server": "Python Test Server",
                "time": time.strftime("%Y-%m-%d %H:%M:%S"),
                "message": "Ready for ESP-01S connections"
            }
            self.wfile.write(json.dumps(response).encode())
        
        elif self.path == '/':
            self.send_response(200)
            self.send_header('Content-type', 'text/html')
            self.end_headers()
            html = """
            <html>
            <body>
                <h1>Test Emergency Server</h1>
                <p>Server is running!</p>
                <p>Endpoints:</p>
                <ul>
                    <li>POST /emergency - For ESP-01S emergency signal</li>
                    <li>GET /status - Check server status</li>
                </ul>
                <p>Time: """ + time.strftime("%Y-%m-%d %H:%M:%S") + """</p>
            </body>
            </html>
            """
            self.wfile.write(html.encode())
    
    def do_POST(self):
        if self.path == '/emergency':
            content_length = int(self.headers['Content-Length'])
            post_data = self.rfile.read(content_length)
            
            print(f"Received emergency: {post_data.decode()}")
            
            self.send_response(200)
            self.send_header('Content-type', 'application/json')
            self.end_headers()
            response = {
                "status": "received",
                "message": "Emergency signal received",
                "timestamp": time.time()
            }
            self.wfile.write(json.dumps(response).encode())

def run_server():
    server_address = ('', 8080)  # Listen on all interfaces, port 8080
    httpd = HTTPServer(server_address, TestHandler)
    print(f"Test server running on http://localhost:8080")
    print(f"Access from ESP-01S using your PC's IP")
    print(f"Check your PC IP with: ipconfig (Windows) or ifconfig (Linux/Mac)")
    httpd.serve_forever()

if __name__ == '__main__':
    run_server()