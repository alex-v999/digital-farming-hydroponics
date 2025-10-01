#include <WiFi.h>
#include <WiFiClientSecure.h> 
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <DHT.h>
#include <OneWire.h>
#include <DallasTemperature.h>

#define MQTT_MAX_PACKET_SIZE 512

// ——— Wi-Fi credentials ————————————————————————
const char* ssid     = "..";
const char* password = "..";

// ——— MQTT (HiveMQ Cloud) —————————————————————
const char* mqttServer = ".....";
const int   mqttPort   = ..;
const char* mqttUser   = "..";
const char* mqttPass   = "..";

// ——— Pin definitions —————————————————————————
#define PH_SENSOR_PIN      34
#define TDS_SENSOR_PIN     35
#define DHTPIN             13
#define DHTTYPE            DHT11
#define ONE_WIRE_BUS       12 // WATER TEMPERATURE SENSOR
#define RELAY_PUMP_PIN     32
#define RELAY_LIGHT_PIN    27
#define RELAY_EXHAUST_PIN  26

// ——— Scheduler variables ———————————————————————
unsigned long runTimePump     = 2700000, offTimePump     = 600000,  lastSwitchPump     = 0;
unsigned long runTimeLight    = 600000,  offTimeLight    = 7200000, lastSwitchLight    = 0;
unsigned long runTimeExhaust  = 300000,  offTimeExhaust  = 1800000, lastSwitchExhaust  = 0;
bool pumpRunning   = false;
bool lightOn       = false;
bool exhaustOn     = false;

bool pumpAuto    = false;
bool lightAuto   = false;
bool exhaustAuto = false;


// ——— Networking clients & sensors —————————————
WiFiClientSecure  net;
PubSubClient      mqtt(net);
DHT               dht(DHTPIN, DHTTYPE);
OneWire           oneWire(ONE_WIRE_BUS);
DallasTemperature waterTempSensor(&oneWire);

unsigned long    lastPublish = 0;
unsigned long    updatePublish = 0;

// ——— Forward declarations —————————————————————
float readPH();
float readTDS();
void  updatePump();
void  updateLight();
void  updateExhaust();
void  mqttCallback(char* topic, byte* payload, unsigned int length);
void  connectMqtt();
void publishStatus();
void publishState();


void setup() {
  Serial.begin(115200);

  // Initialize relays (active LOW)
  pinMode(RELAY_PUMP_PIN,   OUTPUT); digitalWrite(RELAY_PUMP_PIN,   HIGH);
  pinMode(RELAY_LIGHT_PIN,  OUTPUT); digitalWrite(RELAY_LIGHT_PIN,  HIGH);
  pinMode(RELAY_EXHAUST_PIN,OUTPUT); digitalWrite(RELAY_EXHAUST_PIN,HIGH);

  delay(1000);
  
  pinMode(RELAY_PUMP_PIN,   OUTPUT); digitalWrite(RELAY_PUMP_PIN,   LOW);
  pinMode(RELAY_LIGHT_PIN,  OUTPUT); digitalWrite(RELAY_LIGHT_PIN,  LOW);
  pinMode(RELAY_EXHAUST_PIN,OUTPUT); digitalWrite(RELAY_EXHAUST_PIN,LOW);

  // Initialize sensors
  dht.begin();
  waterTempSensor.begin();
  analogReadResolution(10);
  analogSetPinAttenuation(TDS_SENSOR_PIN, ADC_11db);

  // Connect to Wi-Fi
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println("\nWi-Fi connected: " + WiFi.localIP().toString());

  // Setup MQTT over TLS
  net.setInsecure();  // skip cert validation for demo (use CA in production)
  mqtt.setServer(mqttServer, mqttPort);
  mqtt.setCallback(mqttCallback);
  mqtt.setBufferSize(512);

  connectMqtt();
}

void loop() {
  if (!mqtt.connected()) connectMqtt();
  mqtt.loop();


  if(millis() - updatePublish > 100){

    updatePublish = millis();

    updatePump();
    updateLight();
    updateExhaust();

    // inside loop(), after updatePump()/updateLight()/updateExhaust():
    Serial.printf(
      "DEBUG: PUMP[auto=%d running=%d since=%lus run=%lus off=%lus] "
      "LIGHT[auto=%d on=%d since=%lus run=%lus off=%lus] "
      "EXHAUST[auto=%d on=%d since=%lus run=%lus off=%lus]\n",
      pumpAuto, pumpRunning,
        (millis() - lastSwitchPump) / 1000, runTimePump  / 1000, offTimePump  / 1000,
      lightAuto, lightOn,
        (millis() - lastSwitchLight) / 1000, runTimeLight / 1000, offTimeLight / 1000,
      exhaustAuto, exhaustOn,
        (millis() - lastSwitchExhaust) / 1000, runTimeExhaust/ 1000, offTimeExhaust/ 1000
    );
  }

  if (millis() - lastPublish > 5000) {
    lastPublish = millis();
    publishStatus();   // your existing full–JSON publisher
  }
}

void publishState(const char* topic, bool on) {
  mqtt.publish(topic, on ? "ON" : "OFF", true);
}

// ——— MQTT connect & subscriptions —————————————————
void connectMqtt() {
  while (!mqtt.connected()) {
    Serial.print("MQTT connecting…");
    // with this:
    static char clientId[32];
    snprintf(clientId, sizeof(clientId), "ESP32-%08X", (uint32_t)ESP.getEfuseMac());

    if ( mqtt.connect(clientId, mqttUser, mqttPass) ) {
      Serial.println(" connected");
      mqtt.subscribe("home/device1/command/params");
      mqtt.subscribe("home/device1/command/timers");
    } else {
      Serial.print(" failed, rc=");
      Serial.print(mqtt.state());
      Serial.println("; retrying in 5 s");
      delay(5000);
    }
  }
}

// ——— MQTT message handler —————————————————————
void mqttCallback(char* topic, byte* payload, unsigned int length) {
  String t = String(topic);
  String msg;
  msg.reserve(length);
  for (unsigned i = 0; i < length; i++) msg += (char)payload[i];
  msg.trim();

  // — Params CSV: “PUMP:ON:MANUAL,LIGHT:AUTO,EXHAUST:OFF:MANUAL” ——
  if (t.endsWith("/params")) {
    // Parse CSV
    char buf[128];
    msg.toCharArray(buf, sizeof(buf));

    char *saveptr1, *saveptr2;
    for (char *part = strtok_r(buf, ",", &saveptr1);
        part != nullptr;
        part = strtok_r(nullptr, ",", &saveptr1))
    {
      char *device = strtok_r(part, ":", &saveptr2);
      char *a      = strtok_r(nullptr, ":", &saveptr2);  // could be state or AUTO
      char *b      = strtok_r(nullptr, ":", &saveptr2);  // mode if present

      bool manual = false;
      bool on     = false;
      bool autoM  = false;

      if (b) {
        // three‐token form: device:state:mode
        on     = (strcmp(a, "ON")==0);
        manual = (strcmp(b, "MANUAL")==0);
        autoM  = (strcmp(b, "AUTO")==0);
      }
      else if (a) {
        // two‐token form: device:AUTO  or device:MANUAL
        autoM  = (strcmp(a, "AUTO")==0);
        manual = (strcmp(a, "MANUAL")==0);
        // leave `on` untouched when flipping into AUTO
      }

      if (strcmp(device, "PUMP")==0) {
        if (manual) {
          // manual override
          pumpAuto    = false;
          pumpRunning = on;
          digitalWrite(RELAY_PUMP_PIN, pumpRunning ? HIGH : LOW);
        }
        else if (autoM) {
          // enter auto–cycling immediately
          pumpAuto = true;
          // trigger the first ON cycle right now:
          pumpRunning    = true;
          lastSwitchPump = millis();       // reset timer
          digitalWrite(RELAY_PUMP_PIN, HIGH);
        }
      }
      else if (strcmp(device, "LIGHT")==0) {
        if (manual) {
          lightAuto = false;
          lightOn   = on;
          digitalWrite(RELAY_LIGHT_PIN, lightOn ? HIGH : LOW);
        }
        else if (autoM) {
          lightAuto = true;
          lightOn         = true;
          lastSwitchLight = millis();
          digitalWrite(RELAY_LIGHT_PIN, HIGH);
        }
      }
      else if (strcmp(device, "EXHAUST")==0) {
        if (manual) {
          exhaustAuto = false;
          exhaustOn   = on;
          digitalWrite(RELAY_EXHAUST_PIN, exhaustOn ? HIGH : LOW);
        }
        else if (autoM) {
          exhaustAuto = true;
          exhaustOn         = true;
          lastSwitchExhaust = millis();
          digitalWrite(RELAY_EXHAUST_PIN, HIGH);
        }
      }
    }

    publishState("home/device1/command/pump",    pumpRunning);
    publishState("home/device1/command/light",   lightOn);
    publishState("home/device1/command/exhaust", exhaustOn);
  }
  // — Timer updates for AUTO —
  else if (t.endsWith("/timers")) {
    // Enter AUTO for all three
    pumpAuto = lightAuto = exhaustAuto = true;

    // Parse six CSV values (seconds → ms)
    const int EXPECTED = 6;
    int vals[EXPECTED], idx = 0;
    char buf[64];
    msg.toCharArray(buf, sizeof(buf));
    for ( char* tok = strtok(buf, ","); tok && idx < EXPECTED; tok = strtok(NULL, ",") ) {
      vals[idx++] = atoi(tok) * 1000UL;
    }
    if (idx == EXPECTED) {
      runTimePump     = vals[0]; offTimePump     = vals[1];
      runTimeLight    = vals[2]; offTimeLight    = vals[3];
      runTimeExhaust  = vals[4]; offTimeExhaust  = vals[5];
      unsigned long now = millis();
      lastSwitchPump    = now;
      lastSwitchLight   = now;
      lastSwitchExhaust = now;
      Serial.println("New AUTO timers applied");
    }
  }
}

// ——— Helper: publish full JSON status ——————————————————
// ——— Publish only sensor telemetry every 5 s ——————————————————
void publishStatus() {
  StaticJsonDocument<256> doc;
  float tds = readTDS();
  // Sensor readings only
  doc["pH"]          = readPH();                          // float
  doc["tds"]         = tds;                         // float
  doc["ec"]          = tds * 2.0f;                  // float
  doc["ambientTemp"] = dht.readTemperature();             // float
  doc["humidity"]    = dht.readHumidity();                // float

  waterTempSensor.setWaitForConversion(true);
  waterTempSensor.requestTemperatures();
  doc["waterTemp"]   = waterTempSensor.getTempCByIndex(0); // float


  // serialize & publish
  char buf[256];
  size_t len = serializeJson(doc, buf, sizeof(buf));
  mqtt.publish(
    "home/device1/status",
    reinterpret_cast<const uint8_t*>(buf),
    len,
    /*retain=*/false      // no need to retain raw sensor data
  );
  Serial.println("Status ▶ " + String((char*)buf));
}

// ——— Updated scheduler: pump ———————————————————————
void updatePump() {
  if (!pumpAuto) return;
  unsigned long now = millis();
  if (!pumpRunning && now - lastSwitchPump >= offTimePump) {
    pumpRunning = true;
    digitalWrite(RELAY_PUMP_PIN, HIGH);
    lastSwitchPump = now;
    publishState("home/device1/command/pump", true);
  }
  else if (pumpRunning && now - lastSwitchPump >= runTimePump) {
    pumpRunning = false;
    digitalWrite(RELAY_PUMP_PIN, LOW);
    lastSwitchPump = now;
    publishState("home/device1/command/pump", false);
  }
}

void updateLight() {
  if (!lightAuto) return;
  unsigned long now = millis();
  if (!lightOn && now - lastSwitchLight >= offTimeLight) {
    lightOn = true;
    digitalWrite(RELAY_LIGHT_PIN, HIGH);
    lastSwitchLight = now;
    publishState("home/device1/command/light", lightOn);
  }
  else if (lightOn && now - lastSwitchLight >= runTimeLight) {
    lightOn = false;
    digitalWrite(RELAY_LIGHT_PIN, LOW);
    lastSwitchLight = now;
    publishState("home/device1/command/light", lightOn);
  }
}

void updateExhaust() {
  if (!exhaustAuto) return;
  unsigned long now = millis();
  if (!exhaustOn && now - lastSwitchExhaust >= offTimeExhaust) {
    exhaustOn = true;
    digitalWrite(RELAY_EXHAUST_PIN, HIGH);
    lastSwitchExhaust = now;
    publishState("home/device1/command/exhaust", exhaustOn);
  }
  else if (exhaustOn && now - lastSwitchExhaust >= runTimeExhaust) {
    exhaustOn = false;
    digitalWrite(RELAY_EXHAUST_PIN, LOW);
    lastSwitchExhaust = now;
    publishState("home/device1/command/exhaust", exhaustOn);
  }
}


// Helper: read & smooth pH (your original code)
float readPH() {
  const int N = 10;
  int buf[N];
  for(int i = 0; i < N; i++){
    buf[i] = analogRead(PH_SENSOR_PIN);
    delay(5);
  }
  // sort
  for(int i=0;i<N-1;i++) for(int j=i+1;j<N;j++)
    if(buf[j]<buf[i]) { int t=buf[i]; buf[i]=buf[j]; buf[j]=t; }
  // average mid 6
  long sum=0;
  for(int i=2;i<8;i++) sum+=buf[i];
  float avg = sum/6.0;
  float voltage = avg * (3.3/1024.0);
  float correction = 2.0;
  return 7.0 + ((2.5 - voltage)*1.0) + correction;
}

float readTDS() {
  const int N = 10;
  int buf[N];
  for(int i = 0; i < N; i++){
    buf[i] = analogRead(TDS_SENSOR_PIN);
    delay(5);
  }
  // sort
  for(int i=0;i<N-1;i++) for(int j=i+1;j<N;j++)
    if(buf[j]<buf[i]) { int t=buf[i]; buf[i]=buf[j]; buf[j]=t; }
  // average mid 6
  long sum=0;
  for(int i=2;i<8;i++) sum+=buf[i];
  float avg = sum/6.0;
  float voltage = avg * (3.3/1024.0);
  // optional temperature compensation (assume 25 °C)
  float temp = 25.0;
  float comp = 1.0 + 0.02*(temp - 25.0);
  float vComp = voltage / comp;
  // DFRobot cubic fit
  float tds = (133.42 * pow(vComp,3)
               - 255.86 * pow(vComp,2)
               + 857.39 * vComp) * 0.5;
  return tds;
}