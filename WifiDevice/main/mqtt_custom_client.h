
#include <stdio.h>
#include <stdint.h>
#include <string.h>
#include <stdlib.h>

#include <mqtt_client.h>
#include <esp_heap_caps.h>
#include <freertos/FreeRTOS.h>
#include <freertos/task.h>
#include <freertos/portmacro.h>
#include <driver/uart.h>
#include <esp_log.h>

#include "esp_log.h"

#define UART_PORT_NUM UART_NUM_0
#define MQTT_HOSTNAME CONFIG_ESP_MQTT_HOSTNAME
#define MQTT_USERNAME CONFIG_ESP_MQTT_USERNAME
#define MQTT_PASSWORD CONFIG_ESP_MQTT_PASSWORD
#define DEVICE_ID CONFIG_ESP_DEVICE_ID
#define BUF_SIZE 256

static const char * MQTT_CLIENT_TAG = "MQTT_CLIENT";
static const int MQTT_PORT = 1885;
static const char * TEMP_SENSOR_TOPIC = "sensors/temp";
static const char * DISPLAY_TOPIC = "peripherals/lcd_display";
static const char * LED_TOPIC = "peripherals/led0";

static const char * TEMP_SENSOR_CMD = "CMD+TEM=";
static const char * LED_CMD = "CMD+LED=%d>>>";
static const char * DISPLAY_CMD= "CMD+MSG=%s>>>"; 

static const char * ON_DATA = "ON";
static QueueHandle_t uart_queue;
esp_mqtt_client_handle_t mqtt_client; 

static void uart_event_task(void *pvParameters);
static void mqtt_event_handler(void *handler_args, esp_event_base_t base, int32_t event_id, void *event_data);
static esp_err_t mqtt_event_handler_cb(esp_mqtt_event_handle_t event);
static void uart_event_task(void *pvParameters);
static void uart_send(const char * data);
static void uart_init();

static void mqtt_client_init() {
    esp_mqtt_client_config_t config = {
        .host = MQTT_HOSTNAME,
        .username = MQTT_USERNAME,
        .password = MQTT_PASSWORD,
        .port = MQTT_PORT
    };

    mqtt_client = esp_mqtt_client_init(&config);
    esp_mqtt_client_register_event(mqtt_client, ESP_EVENT_ANY_ID, mqtt_event_handler, mqtt_client);
    esp_mqtt_client_start(mqtt_client);
}

static void mqtt_event_handler (void *handler_args, esp_event_base_t base, int32_t event_id, void *event_data) {
    ESP_LOGD(MQTT_CLIENT_TAG, "Event dispatched from event loop base=%s, event_id=%d", base, event_id);
    mqtt_event_handler_cb(event_data);
}

static esp_err_t mqtt_event_handler_cb (esp_mqtt_event_handle_t event) {
    esp_mqtt_client_handle_t client = event->client;
    char buffer_data[512];
    char buffer_topic[30];
    char * uart_msg;
    uint8_t msg_len;
    
    switch (event->event_id)
    {
    case MQTT_EVENT_CONNECTED:
        uart_init();
        esp_mqtt_client_subscribe(client, LED_TOPIC, 1);
        esp_mqtt_client_subscribe(client, DISPLAY_TOPIC, 1);
        break;
    
    case MQTT_EVENT_DATA:
        memset(buffer_data, '\0', sizeof(buffer_data));
        memset(buffer_topic, '\0', event->topic_len);
        memcpy(buffer_data, event->data, event->data_len);
        memcpy(buffer_topic, event->topic, event->topic_len);
        
        if (strcmp(buffer_topic, LED_TOPIC) == 0) {
            int value = (strcmp(buffer_data, ON_DATA) == 0) ? 1 : 0;
            msg_len = strlen(LED_CMD);
            uart_msg = malloc(msg_len);
            bzero(uart_msg, msg_len);
            sprintf(uart_msg, LED_CMD, value);
            uart_send(uart_msg);
            free(uart_msg);
            uart_msg = NULL;
        } else if(strcmp(buffer_topic, DISPLAY_TOPIC) == 0) {
            msg_len = strlen(DISPLAY_CMD) + strlen(buffer_data);
            uart_msg = malloc(msg_len);
            bzero(uart_msg, msg_len);
            sprintf(uart_msg, "%s: %s", DISPLAY_CMD, buffer_data);
            uart_send(uart_msg);
            free(uart_msg);
            uart_msg = NULL;
        }
        
        break;

    case MQTT_EVENT_ERROR:
        break;

    case MQTT_EVENT_DISCONNECTED:
        break;

    case MQTT_EVENT_SUBSCRIBED:
        break;

    case MQTT_EVENT_PUBLISHED:
        break;

    case MQTT_EVENT_BEFORE_CONNECT:
        break;

    case MQTT_EVENT_UNSUBSCRIBED:
        break;
    }

    return ESP_OK;
}

static void uart_init() {
    uart_config_t config = {
        .baud_rate = 9600,
        .data_bits = UART_DATA_8_BITS,
        .parity = UART_PARITY_DISABLE,
        .stop_bits = UART_STOP_BITS_1,
        .flow_ctrl = UART_HW_FLOWCTRL_DISABLE
    };

    uart_param_config(UART_PORT_NUM, &config);
    uart_driver_install(UART_PORT_NUM, BUF_SIZE * 2, BUF_SIZE * 2, 100, &uart_queue, 0);
    xTaskCreate(uart_event_task, "uart_event_task", 2048, NULL, 12, NULL);
}

static void uart_send(const char * data) {
    uart_write_bytes(UART_PORT_NUM, data, strlen(data));
}

static void uart_event_task(void *pvParameters) {
    uart_event_t event;
    uint8_t * dtmp = (uint8_t *) malloc(BUF_SIZE);

    while (true) {
        if (xQueueReceive(uart_queue, (void * )&event, (portTickType)portMAX_DELAY)) {
            bzero(dtmp, BUF_SIZE);

            switch (event.type) {
            case UART_DATA:
                uart_read_bytes(UART_PORT_NUM, dtmp, event.size, portMAX_DELAY);
                char *delimiter = ">>>";
                char *token;

                token = strtok((char*)dtmp, delimiter);

                while (token != NULL) {
                    char *tempData = strstr((char*)dtmp, TEMP_SENSOR_CMD);

                    if(tempData != NULL ) {
                        int temperature;

                        sscanf(tempData, "CMD+TEM=%d", &temperature);
                        uint8_t * str_temperature = (uint8_t *) malloc(46);
                        sprintf((char *)str_temperature, "%d %s", temperature, DEVICE_ID);
                        esp_mqtt_client_publish(mqtt_client, TEMP_SENSOR_TOPIC, (char *)str_temperature, 0, 1, false);
                        free(str_temperature);
                        str_temperature = NULL;                        
                    }

                    token = strtok(NULL, delimiter);
                }

                break;
            
            case UART_FIFO_OVF:
                ESP_LOGI(TAG, "hw fifo overflow");
                uart_flush_input(UART_PORT_NUM);
                xQueueReset(uart_queue);
                break;

            case UART_BUFFER_FULL:
                ESP_LOGI(TAG, "ring buffer full");
                uart_flush_input(UART_PORT_NUM);
                xQueueReset(uart_queue);
                break;

            case UART_PARITY_ERR:
                ESP_LOGI(TAG, "uart parity error");
                break;

            case UART_FRAME_ERR:
                ESP_LOGI(TAG, "uart frame error");
                break;

            default:
                ESP_LOGI(TAG, "uart event type: %d", event.type);
                break;
            }
        }
    }

    free(dtmp);
    dtmp = NULL;
    vTaskDelete(NULL);
}