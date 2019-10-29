#include <stdio.h>
#include <stdlib.h>
#include "xparameters.h"
#include "xstatus.h"
#include "xil_exception.h"
#include "xttcps.h"
#include "xscugic.h"
#include "xil_printf.h"
#include "xuartps.h"

/************************** Command Definition *******************************/
#define COMMAND_ID_MASK							0xFF

#define COMMAND_SET_SINGLE_LED					0x01

#define COMMAND_SET_MULTIPLE_LEDS 				0x02
#define COMMAND_SET_MULTIPLE_LEDS_NUMBER_MASK	0xFF00
#define COMMAND_SET_MULTIPLE_LEDS_NUMBER_OFFSET	8

#define COMMAND_RESPONSE_OK						0xAA
/************************** Constant Definitions *****************************/
/*
 * The following constants map to the XPAR parameters created in the
 * xparameters.h file. They are only defined here such that a user can easily
 * change all the needed parameters in one place.
 */
#define TTC_PWM_DEVICE_ID	XPAR_XTTCPS_0_DEVICE_ID
#define TTC_PWM_INTR_ID		XPAR_XTTCPS_0_INTR
#define TTCPS_CLOCK_HZ		XPAR_XTTCPS_0_CLOCK_HZ
#define INTC_DEVICE_ID		XPAR_SCUGIC_SINGLE_DEVICE_ID

/*
 * Constants to set the basic operating parameters.
 */
#define	PWM_OUT_FREQ		800000  /* PWM timer counter's output frequency */

/**************************** Type Definitions *******************************/
typedef struct {
	u32 OutputHz;	/* Output frequency */
	XInterval Interval;	/* Interval value */
	u8 Prescaler;	/* Prescaler value */
	u16 Options;	/* Option settings */
} TmrCntrSetup;

/***************** Macros (Inline Functions) Definitions *********************/


/************************** Function Prototypes ******************************/

static int TmrInterruptExample(void);  /* Main test */

/* Set up routines for timer counters */
static int SetupPWM(void);
static int SetupTimer(int DeviceID);
static void SetPwmMatchValue(void);
/* Interleaved interrupt test for both timer counters */
static int MainProgram(void);
static int HandleCommands(u32 command, u8 data);
static int SetupInterruptSystem(u16 IntcDeviceID, XScuGic *IntcInstancePtr);
static void PWMHandler(void *CallBackRef);

/************************** Variable Definitions *****************************/

static XTtcPs TtcPsInst[2];	/* Two timer counters */

static TmrCntrSetup SettingsTable[1] = {
	{PWM_OUT_FREQ, 0, 0, 0}, /* PWM timer counter initial setup, only output freq */
};

XScuGic InterruptController;  /* Interrupt controller instance */

static u32 MatchValue;  /* Match value for PWM, set by PWM interrupt handler,
			updated by main test routine */

static volatile u32 PWM_UpdateFlag;	/* Flag used by Ticker to signal PWM */
static volatile u8 ErrorCount;		/* Errors seen at interrupt time */
static volatile u32 TickCount;		/* Ticker interrupts between PWM change */
static volatile u32 nextMatchValue;

typedef union {
	struct {
		u8 green;
		u8 red;
		u8 blue;
		u8 reserved;
	} led;
	u32 color;
}LedType;

enum CommandReceiveState { Command, Data };

#define MAX_LED_NUMBER 144

static const u32 DutyCycles[2] = {33, 66};
static volatile u32 MatchValues[2];
static volatile u8 BitCount = 7;
static volatile u8 ByteCount = 0;
XTtcPs* Timer = &(TtcPsInst[TTC_PWM_DEVICE_ID]);
static LedType Leds[MAX_LED_NUMBER];
static volatile LedType* currentLed = &Leds[0];
enum CommandReceiveState commandState = Command;
static u8 PwmStarted = 0;

/*****************************************************************************/
/**
*
* This function calls the Ttc interrupt example.
*
* @param	None
*
* @return
*		- XST_SUCCESS to indicate Success
*		- XST_FAILURE to indicate Failure.
*
* @note		None
*
*****************************************************************************/
int main(void)
{
	int Status;

	xil_printf("TTC Interrupt Example Test\r\n");

	Status = TmrInterruptExample();
	if (Status != XST_SUCCESS) {
		xil_printf("TTC Interrupt Example Test Failed\r\n");
		return XST_FAILURE;
	}

	xil_printf("Successfully ran TTC Interrupt Example Test\r\n");
	return XST_SUCCESS;
}

/*****************************************************************************/
/**
*
* This function sets up the interrupt example.
*
* @param	None.
*
* @return
*		- XST_SUCCESS to indicate Success
*		- XST_FAILURE to indicate Failure.
*
* @note		None
*
****************************************************************************/
static int TmrInterruptExample(void)
{
	int Status;

	/*
	 * Make sure the interrupts are disabled, in case this is being run
	 * again after a failure.
	 */

	/*
	 * Connect the Intc to the interrupt subsystem such that interrupts can
	 * occur. This function is application specific.
	 */
	Status = SetupInterruptSystem(INTC_DEVICE_ID, &InterruptController);
	if (Status != XST_SUCCESS) {
		return XST_FAILURE;
	}

	/*
	 * Set up  the PWM timer
	 */
	Status = SetupPWM();
	if (Status != XST_SUCCESS) {
		return Status;
	}

	Status = MainProgram();
	if (Status != XST_SUCCESS) {
		return Status;
	}

	XTtcPs_Stop(&(TtcPsInst[TTC_PWM_DEVICE_ID]));

	return XST_SUCCESS;
}

/****************************************************************************/
/**
*
* This function sets up the waveform output timer counter (PWM).
*
* @param	None
*
* @return	XST_SUCCESS if everything sets up well, XST_FAILURE otherwise.
*
* @note		None
*
*****************************************************************************/
int SetupPWM(void)
{
	int Status;
	TmrCntrSetup *TimerSetup;
	XTtcPs *TtcPsPWM;

	TimerSetup = &(SettingsTable[TTC_PWM_DEVICE_ID]);

	/*
	 * Set up appropriate options for PWM: interval mode  and
	 * match mode for waveform output.
	 */
	TimerSetup->Options |= (XTTCPS_OPTION_INTERVAL_MODE |
					      XTTCPS_OPTION_MATCH_MODE | XTTCPS_OPTION_WAVE_POLARITY);

	/*
	 * Calling the timer setup routine
	 * 	initialize device
	 * 	set options
	 */
	Status = SetupTimer(TTC_PWM_DEVICE_ID);
	if(Status != XST_SUCCESS) {
		return Status;
	}

	TtcPsPWM = &(TtcPsInst[TTC_PWM_DEVICE_ID]);

	/*
	 * Connect to the interrupt controller
	 */
	Status = XScuGic_Connect(&InterruptController, TTC_PWM_INTR_ID,
		(Xil_ExceptionHandler)PWMHandler, (void *)&MatchValue);
	if (Status != XST_SUCCESS) {
		return XST_FAILURE;
	}

	/*
	 * Enable the interrupt for the Timer counter
	 */
	XScuGic_Enable(&InterruptController, TTC_PWM_INTR_ID);

	/*
	 * Enable the interrupts for the tick timer/counter
	 * We only care about the interval timeout.
	 */
	XTtcPs_EnableInterrupts(TtcPsPWM, XTTCPS_IXR_INTERVAL_MASK);

	return Status;
}
/****************************************************************************/
/**
*
* This function uses the interrupt inter-locking between the ticker timer
* counter and the waveform output timer counter. When certain amount of
* interrupts have happened to the ticker timer counter, a flag, PWM_UpdateFlag,
* is set to true.
*
* When PWM_UpdateFlag for the waveform timer counter is true, the duty
* cycle for PWM timer counter is increased by PWM_DELTA_DUTY.
* The function exits successfully when the duty cycle for PWM timer
* counter reaches beyond 100.
*
* @param	None
*
* @return	XST_SUCCESS if duty cycle successfully reaches beyond 100,
*		otherwise, XST_FAILURE.
*
* @note		None.
*
*****************************************************************************/
int MainProgram(void)
{
	TmrCntrSetup *TimerSetup;
	XTtcPs *TtcPs_PWM;	/* Pointer to the instance structure */

	TimerSetup = &(SettingsTable[TTC_PWM_DEVICE_ID]);
	TtcPs_PWM = &(TtcPsInst[TTC_PWM_DEVICE_ID]);

	MatchValues[0] = ((u32) TimerSetup->Interval * (u32) DutyCycles[0]) / 100;
	MatchValues[1] = ((u32) TimerSetup->Interval * (u32) DutyCycles[1]) / 100;

	// Fill LEDs with default pattern (color gradient BLUE -> RED)
	for(uint i = 0; i < MAX_LED_NUMBER; i++) {
		double val = (double) i / MAX_LED_NUMBER;
		u32 led = 0 << 0 | (uint) (255.0 * val) << 8 | (uint) (255.0*(1.0 - val)) << 16;
		Leds[i].color = led;
	}

	XTtcPs_EnableInterrupts(TtcPs_PWM, XTTCPS_IXR_INTERVAL_MASK);

	//XUartPs_SetBaudRate(XUartPs *InstancePtr, u32 BaudRate);
	while (XUartPs_IsReceiveData(XPAR_PS7_UART_1_BASEADDR))
	{
		XUartPs_ReadReg(XPAR_PS7_UART_1_BASEADDR, XUARTPS_FIFO_OFFSET);
	}


	u8 uartByteCount = 0;
	u32 command;
	//XTtcPs_Start(Timer);
	StartPwm();

	while(1)
	{
		u8 data = 0;
		if (XUartPs_IsReceiveData(XPAR_PS7_UART_1_BASEADDR))
		{
			data = XUartPs_ReadReg(XPAR_PS7_UART_1_BASEADDR, XUARTPS_FIFO_OFFSET);
			switch(commandState) {
			case Command:
				command = command << 8 | data;
				uartByteCount++;
				if(uartByteCount >= 4) {
					uartByteCount = 0;
					commandState = Data;
				}
				break;

			case Data:
				HandleCommands(command, data);
				break;
			}
			//xil_printf("%c", data);
		}
	}

	return XST_SUCCESS;
}

int HandleCommands(u32 command, u8 data) {
	//static u8 dataBuffer[1024] = {0};
	static u32 dataCount = 0;
	static u32 lastIndex = 0;
	u8 reset = 0;
	//dataBuffer[dataCount] = data;


	switch(command & COMMAND_ID_MASK) {
	case COMMAND_SET_SINGLE_LED:

		break;

	case COMMAND_SET_MULTIPLE_LEDS:
		// Data contains number of LEDs -> 3 byte per Led
		if(dataCount == 0) {
			lastIndex = (((command & COMMAND_SET_MULTIPLE_LEDS_NUMBER_MASK) >> COMMAND_SET_MULTIPLE_LEDS_NUMBER_OFFSET) << 2) - 1;
		}
		LedType* led = &Leds[(dataCount) >> 2];
		led->color = (led->color & (~(0x000000FF << (((dataCount) & 3) * 8)))) | (data << (((dataCount) & 3) * 8));

		if(dataCount >= lastIndex) {
			reset = 1;
		}
		break;
	}

	if(reset) {
		dataCount = 0;
		StartPwm();
		commandState = Command;
		XUartPs_SendByte(XPAR_PS7_UART_1_BASEADDR, COMMAND_RESPONSE_OK);
	}
	else {
		dataCount++;
	}

	return 0;
}

void StartPwm() {
	while(PwmStarted);
	PwmStarted = 1;
	SetPwmMatchValue();
	XTtcPs_SetMatchValue(Timer, 0, nextMatchValue);
	XTtcPs_EnableInterrupts(Timer, XTTCPS_IXR_INTERVAL_MASK);
	//usleep(1000);
	XTtcPs_Start(Timer);
}

/****************************************************************************/
/**
*
* This function sets up a timer counter device, using the information in its
* setup structure.
*  . initialize device
*  . set options
*  . set interval and prescaler value for given output frequency.
*
* @param	DeviceID is the unique ID for the device.
*
* @return	XST_SUCCESS if successful, otherwise XST_FAILURE.
*
* @note		None.
*
*****************************************************************************/
int SetupTimer(int DeviceID)
{
	int Status;
	XTtcPs_Config *Config;
	XTtcPs *Timer;
	TmrCntrSetup *TimerSetup;

	TimerSetup = &SettingsTable[DeviceID];

	Timer = &(TtcPsInst[DeviceID]);

	/*
	 * Look up the configuration based on the device identifier
	 */
	Config = XTtcPs_LookupConfig(DeviceID);
	if (NULL == Config) {
		return XST_FAILURE;
	}

	/*
	 * Initialize the device
	 * Stop timer if necessary
	 */
	Status = XST_DEVICE_IS_STARTED;
	while(XST_DEVICE_IS_STARTED == Status) {
		Status = XTtcPs_CfgInitialize(Timer, Config, Config->BaseAddress);
		if(Status == XST_DEVICE_IS_STARTED) {
			XTtcPs_Stop(Timer);
			continue;
		}
		if (Status != XST_SUCCESS) {
			return XST_FAILURE;
		}
	}
	/*
	 * Set the options
	 */
	XTtcPs_SetOptions(Timer, TimerSetup->Options);

	/*
	 * Timer frequency is preset in the TimerSetup structure,
	 * however, the value is not reflected in its other fields, such as
	 * IntervalValue and PrescalerValue. The following call will map the
	 * frequency to the interval and prescaler values.
	 */
	XTtcPs_CalcIntervalFromFreq(Timer, TimerSetup->OutputHz,
		&(TimerSetup->Interval), &(TimerSetup->Prescaler));

	/*
	 * Set the interval and prescale
	 */
	XTtcPs_SetInterval(Timer, TimerSetup->Interval);
	XTtcPs_SetPrescaler(Timer, TimerSetup->Prescaler);

	return XST_SUCCESS;
}

/****************************************************************************/
/**
*
* This function setups the interrupt system such that interrupts can occur.
* This function is application specific since the actual system may or may not
* have an interrupt controller.  The TTC could be directly connected to a
* processor without an interrupt controller.  The user should modify this
* function to fit the application.
*
* @param	IntcDeviceID is the unique ID of the interrupt controller
* @param	IntcInstacePtr is a pointer to the interrupt controller
*		instance.
*
* @return	XST_SUCCESS if successful, otherwise XST_FAILURE.
*
* @note		None.
*
*****************************************************************************/
static int SetupInterruptSystem(u16 IntcDeviceID,
				    XScuGic *IntcInstancePtr)
{
	int Status;
	XScuGic_Config *IntcConfig; /* The configuration parameters of the
					interrupt controller */

	/*
	 * Initialize the interrupt controller driver
	 */
	IntcConfig = XScuGic_LookupConfig(IntcDeviceID);
	if (NULL == IntcConfig) {
		return XST_FAILURE;
	}

	Status = XScuGic_CfgInitialize(IntcInstancePtr, IntcConfig,
					IntcConfig->CpuBaseAddress);
	if (Status != XST_SUCCESS) {
		return XST_FAILURE;
	}

	/*
	 * Connect the interrupt controller interrupt handler to the hardware
	 * interrupt handling logic in the ARM processor.
	 */
	Xil_ExceptionRegisterHandler(XIL_EXCEPTION_ID_INT,
			(Xil_ExceptionHandler) XScuGic_InterruptHandler,
			IntcInstancePtr);

	/*
	 * Enable interrupts in the ARM
	 */
	Xil_ExceptionEnable();

	return XST_SUCCESS;
}

static void SetPwmMatchValue(void)
{
	if(BitCount >= 8)
	{
		BitCount = 7;
		if(++ByteCount >= 3)
		{
			ByteCount = 0;
			if(++currentLed > &Leds[MAX_LED_NUMBER - 1]) {
				currentLed = &Leds[0];
				XTtcPs_DisableInterrupts(Timer, XTTCPS_IXR_INTERVAL_MASK);
				//XTtcPs_WriteReg(Timer->Config.BaseAddress, XTTCPS_CNT_CNTRL_OFFSET, 0x11);
				XTtcPs_Stop(Timer);
				XTtcPs_ResetCounterValue(Timer);
				PwmStarted = 0;
				u8* colorPtr = ((u8*) currentLed) + ByteCount;
				if(*colorPtr & (1 << BitCount)) {
					nextMatchValue = MatchValues[1];
				}
				else {
					nextMatchValue = MatchValues[0];
				}
				return;
			}
		}
	}

	u8* colorPtr = ((u8*) currentLed) + ByteCount;
	if(*colorPtr & (1 << BitCount)) {
		nextMatchValue = MatchValues[1];
	}
	else {
		nextMatchValue = MatchValues[0];
	}
	BitCount--;
}

/***************************************************************************/
/**
*
* This function is the handler which handles the PWM interrupt.
*
* It updates the match register to reflect the change on duty cycle. It also
* disable interrupt at the end. The interrupt will be enabled by the Ticker
* timer counter.
*
* @param	CallBackRef contains a callback reference from the driver, in
*		this case it is a pointer to the MatchValue variable.
*
* @return	None.
*
* @note		None.
*
*****************************************************************************/
static void PWMHandler(void *CallBackRef)
{
	//u32 *MatchReg;
	u32 StatusEvent;
	/*
	 * Read the interrupt status, then write it back to clear the interrupt.
	 */
	XTtcPs_SetMatchValue(Timer, 0, nextMatchValue);
	SetPwmMatchValue();
	StatusEvent = XTtcPs_GetInterruptStatus(Timer);
	XTtcPs_ClearInterruptStatus(Timer, StatusEvent);
}
