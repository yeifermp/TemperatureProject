
// PIC18F4550 Configuration Bit Settings

// 'C' source line config statements

// CONFIG1L
#pragma config PLLDIV = 4       // PLL Prescaler Selection bits (Divide by 2 (8 MHz oscillator input))
#pragma config CPUDIV = OSC1_PLL2// System Clock Postscaler Selection bits ([Primary Oscillator Src: /1][96 MHz PLL Src: /2])
#pragma config USBDIV = 1       // USB Clock Selection bit (used in Full-Speed USB mode only; UCFG:FSEN = 1) (USB clock source comes directly from the primary oscillator block with no postscale)

// CONFIG1H
#pragma config FOSC = HS        // Oscillator Selection bits (Internal oscillator, port function on RA6, EC used by USB (INTIO))
#pragma config FCMEN = OFF      // Fail-Safe Clock Monitor Enable bit (Fail-Safe Clock Monitor disabled)
#pragma config IESO = OFF       // Internal/External Oscillator Switchover bit (Oscillator Switchover mode disabled)

// CONFIG2L
#pragma config PWRT = OFF       // Power-up Timer Enable bit (PWRT disabled)
#pragma config BOR = OFF         // Brown-out Reset Enable bits (Brown-out Reset enabled in hardware only (SBOREN is disabled))
#pragma config BORV = 3         // Brown-out Reset Voltage bits (Minimum setting 2.05V)
#pragma config VREGEN = OFF     // USB Voltage Regulator Enable bit (USB voltage regulator disabled)

// CONFIG2H
#pragma config WDT = OFF         // Watchdog Timer Enable bit (WDT enabled)
#pragma config WDTPS = 32768    // Watchdog Timer Postscale Select bits (1:32768)

// CONFIG3H
#pragma config CCP2MX = ON      // CCP2 MUX bit (CCP2 input/output is multiplexed with RC1)
#pragma config PBADEN = ON      // PORTB A/D Enable bit (PORTB<4:0> pins are configured as analog input channels on Reset)
#pragma config LPT1OSC = OFF    // Low-Power Timer 1 Oscillator Enable bit (Timer1 configured for higher power operation)
#pragma config MCLRE = OFF       // MCLR Pin Enable bit (MCLR pin enabled; RE3 input pin disabled)

// CONFIG4L
#pragma config STVREN = OFF      // Stack Full/Underflow Reset Enable bit (Stack full/underflow will cause Reset)
#pragma config LVP = OFF         // Single-Supply ICSP Enable bit (Single-Supply ICSP enabled)
#pragma config ICPRT = OFF      // Dedicated In-Circuit Debug/Programming Port (ICPORT) Enable bit (ICPORT disabled)
#pragma config XINST = OFF      // Extended Instruction Set Enable bit (Instruction set extension and Indexed Addressing mode disabled (Legacy mode))

// CONFIG5L
#pragma config CP0 = OFF        // Code Protection bit (Block 0 (000800-001FFFh) is not code-protected)
#pragma config CP1 = OFF        // Code Protection bit (Block 1 (002000-003FFFh) is not code-protected)
#pragma config CP2 = OFF        // Code Protection bit (Block 2 (004000-005FFFh) is not code-protected)
#pragma config CP3 = OFF        // Code Protection bit (Block 3 (006000-007FFFh) is not code-protected)

// CONFIG5H
#pragma config CPB = OFF        // Boot Block Code Protection bit (Boot block (000000-0007FFh) is not code-protected)
#pragma config CPD = OFF        // Data EEPROM Code Protection bit (Data EEPROM is not code-protected)

// CONFIG6L
#pragma config WRT0 = OFF       // Write Protection bit (Block 0 (000800-001FFFh) is not write-protected)
#pragma config WRT1 = OFF       // Write Protection bit (Block 1 (002000-003FFFh) is not write-protected)
#pragma config WRT2 = OFF       // Write Protection bit (Block 2 (004000-005FFFh) is not write-protected)
#pragma config WRT3 = OFF       // Write Protection bit (Block 3 (006000-007FFFh) is not write-protected)

// CONFIG6H
#pragma config WRTC = OFF       // Configuration Register Write Protection bit (Configuration registers (300000-3000FFh) are not write-protected)
#pragma config WRTB = OFF       // Boot Block Write Protection bit (Boot block (000000-0007FFh) is not write-protected)
#pragma config WRTD = OFF       // Data EEPROM Write Protection bit (Data EEPROM is not write-protected)

// CONFIG7L
#pragma config EBTR0 = OFF      // Table Read Protection bit (Block 0 (000800-001FFFh) is not protected from table reads executed in other blocks)
#pragma config EBTR1 = OFF      // Table Read Protection bit (Block 1 (002000-003FFFh) is not protected from table reads executed in other blocks)
#pragma config EBTR2 = OFF      // Table Read Protection bit (Block 2 (004000-005FFFh) is not protected from table reads executed in other blocks)
#pragma config EBTR3 = OFF      // Table Read Protection bit (Block 3 (006000-007FFFh) is not protected from table reads executed in other blocks)

// CONFIG7H
#pragma config EBTRB = OFF      // Boot Block Table Read Protection bit (Boot block (000000-0007FFh) is not protected from table reads executed in other blocks)

// #pragma config statements should precede project file includes.
// Use project enums instead of #define for ON and OFF.

#include <xc.h>
#include <pic18f4553.h>
#include <stdint.h>
#include <string.h>
#include <stdint.h>
#include <stdbool.h>
#include <stdlib.h>
#include <stdio.h>

#define _XTAL_FREQ 16000000
#define BUF_SIZE 512
#define BUF_SIZE_OUT 50
#define DHT_DATA_IN PORTDbits.RD2
#define DHT_DATA_OUT LATDbits.LATD2

enum ACTION { 
    LED_ON,
    LED_OFF, 
    NO_OP 
};

typedef enum ACTION action_t;

void configureOscilator(void);
void delay1Second(void);
void USART_Init(void);
bool is_command(const char * command);
action_t get_action (void);
bool data_received(void);
void USART_SendChar(char data);
void USART_SendString(char * data);
int dht_read_bit(void);
void dht_read(void);

char data_in[BUF_SIZE];
char data_out[50];
int dht_raw_data[40];
uint8_t position;
static const char * TEMP_SENSOR_CMD = "CMD+TEM=%d>>>CMD+RH=%d>>>";
static const char * LED0_CMD = "CMD+LED";
static const char * END_CMD = ">>>";

void main(void) {
    USART_Init();
    memset(data_in, 0, BUF_SIZE);    
    position = 0;       
    TRISD = 0;
    LATD = 0;  
    
    while (1) {    
        __delay_ms(1000);
        dht_read();
        
        if (data_received()) {
            action_t action = get_action();
            
            switch (action) {
                case LED_ON:
                    LATDbits.LATD1 = 1;
                    break;
                    
                case LED_OFF:
                    LATDbits.LATD1 = 0;
                    break;
                    
                case NO_OP:
                    break;
            }
        }
    }
    
    return;
}

void dht_read() {
    memset(dht_raw_data, 0, 40);  
    TRISD = 0;
    DHT_DATA_OUT = 0;
    __delay_ms(18);
    DHT_DATA_OUT = 1;
    __delay_us(20);
    TRISD = 1;
    
    while (DHT_DATA_IN);      
    while (!DHT_DATA_IN);
    while (DHT_DATA_IN);
    
    for (uint8_t index = 0; index < 40; index++) {
        dht_raw_data[index] = dht_read_bit();
    }
    
    int relative_humidity = 0;
    int temperature = 0;
    int mask = 0;
    int bit_index = 0;
    int bit_position = 7;
    
    for (bit_index = 0; bit_index < 8; bit_index++) {
        mask = dht_raw_data[bit_index];
        
        mask = (mask << bit_position);
        relative_humidity = (relative_humidity | mask);
        bit_position--;
    }
    
    bit_position = 7;
    
    for (bit_index = 16;  bit_index < 24; bit_index++) {
        mask = dht_raw_data[bit_index];
        
        mask = (mask << bit_position);
        temperature = (temperature | mask);
        bit_position--;
    }
    
    memset(data_out, 0, BUF_SIZE_OUT);
    sprintf(data_out, TEMP_SENSOR_CMD, temperature, relative_humidity);
    USART_SendString(data_out);
}

int dht_read_bit() {
    while (!DHT_DATA_IN);
    __delay_us(30);
    
    if (DHT_DATA_IN == 1) {
        while(DHT_DATA_IN);
        return 1;
    } else {
        return 0;
    }
}

bool data_received() {
    char * ptr = strstr(data_in, END_CMD);
            
    if (ptr != NULL) {
        return true;
    }
    
    return false;
}

action_t get_action() {    
    if (is_command(LED0_CMD)) {
        char * str_value = (strstr(data_in, "=") + 1);
        int value = atoi(str_value);
        memset(data_in, '\0', BUF_SIZE);
        position = 0;
    
        return (value == 1) ? LED_ON : LED_OFF;
    }
    
    
    
    return NO_OP;
}

bool is_command(const char * command) {
    char * ptr =  strstr(data_in, command);
    
    if (ptr != NULL) {
        return true;
    }
    
    return false;
}

void USART_Init() {
    TRISC6=1;
    TRISC7=1;
    SPBRG = 25;
    TXSTAbits.CSRC = 1;
    TXSTAbits.TXEN = 1;
    RCSTAbits.CREN = 1;
    RCSTAbits.SPEN = 1;
    
    INTCONbits.GIE = 1;
    INTCONbits.PEIE = 1;
    PIE1bits.RCIE = 1;
    PIE1bits.TXIE = 0;
}

void __interrupt() isr(void) {
    if (RCIF == 1) {
        if (RCSTAbits.OERR) {
            CREN = 0;
            NOP();
            CREN=1;
        }
        
        data_in[position] = RCREG;
        position++;
    }
}

void USART_SendChar(char data) {
    while (TXIF == 0);
    TXREG = data;
}

void USART_SendString(char * data) {
    for (uint8_t index = 0; index < strlen(data); index++) {
        USART_SendChar(data[index]);
    }
}